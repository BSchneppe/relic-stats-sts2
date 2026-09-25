#if DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;
using Environment = System.Environment;

namespace RelicStats.Core.Testing;

public static class TestManager
{
    private static TestRunner? _activeRunner;
    private static readonly Queue<string> _testQueue = new();
    private static readonly List<(string RelicId, TestResult Result)> _results = new();
    private static Action<string>? _onAllComplete;
    // True from the moment a run starts until EndRun, including the gaps between tests, so a
    // second "relicstats test" cannot start in the middle of a run.
    private static bool _runActive;
    public static bool IsRunning => _runActive;

    /// <summary>The player's fast mode while a run forces Instant; null when no run holds it.</summary>
    internal static FastModeType? SavedFastMode => _savedFastMode;

    // A room transition running this long is stuck (an event exit waiting on an option that will
    // never resolve); every later test would fail behind it, so the run is aborted instead.
    private const double StuckTransitionSeconds = 15;
    private static SceneTreeTimer? _tickTimer;

    public static IReadOnlyList<(string RelicId, TestResult Result)> LastResults => _results;

    private static void StripPlayerRelics()
    {
        if (TestHelpers.Player == null) return;
        foreach (var relic in TestHelpers.Player.Relics.ToArray())
            TestHelpers.Player.RemoveRelicInternal(relic, silent: true);
    }

    private static void ClearPlayerDeck()
    {
        if (TestHelpers.Player == null) return;
        PileType.Deck.GetPile(TestHelpers.Player).Clear(silent: true);
    }


    // The player's fast-mode preference, saved while a test run forces Instant.
    private static FastModeType? _savedFastMode;

    private static void BeginRun(Action<string>? onComplete)
    {
        _results.Clear();
        _testQueue.Clear();
        _onAllComplete = onComplete;
        _runActive = true;
        StripPlayerRelics();
        ClearPlayerDeck();
        EnableInstantMode();
    }

    /// <summary>
    /// The single exit of a run: persists failures, gives the player their fast mode back and
    /// reports. Every path that ends a run — all tests done, an aborted run, a test that threw —
    /// comes through here, so a run can never leave Instant mode behind.
    /// </summary>
    private static void EndRun(string? abortReason = null)
    {
        _testQueue.Clear();
        PersistFailedTests();
        RestoreFastMode();
        _runActive = false;

        var passed = _results.Count(r => r.Result.Passed);
        var failed = _results.Count - passed;
        var summary = $"{passed} passed, {failed} failed (of {_results.Count})";
        if (abortReason != null) summary += $" — run aborted: {abortReason}";
        MainFile.Logger.Info($"Test run complete: {summary}");
        _onAllComplete?.Invoke(summary);
    }

    /// <summary>
    /// Runs the whole test run with the game's Instant fast mode: every Cmd.Wait (card and power
    /// animations, the 1s relic flashes, turn transitions) returns immediately instead of waiting
    /// on a timer. Without it most of a run's wall time is animation. The player's own setting is
    /// restored when the run completes.
    /// </summary>
    private static void EnableInstantMode()
    {
        var prefs = SaveManager.Instance?.PrefsSave;
        if (prefs == null) return;
        _savedFastMode ??= prefs.FastMode;
        prefs.FastMode = FastModeType.Instant;
        MainFile.Logger.Info($"[TestManager] fast mode Instant for the test run (was {_savedFastMode})");
    }

    private static void RestoreFastMode()
    {
        var prefs = SaveManager.Instance?.PrefsSave;
        if (prefs == null || _savedFastMode == null) return;
        prefs.FastMode = _savedFastMode.Value;
        MainFile.Logger.Info($"[TestManager] fast mode restored to {_savedFastMode}");
        _savedFastMode = null;
    }

    public static void RunSingle(string relicId, Action<string>? onComplete = null)
    {
        BeginRun(onComplete);
        StartTest(relicId);
    }

    public static void RunFailed(Action<string>? onComplete = null)
    {
        var failedIds = _results.Where(r => !r.Result.Passed).Select(r => r.RelicId).ToList();
        if (failedIds.Count == 0)
            failedIds = LoadPersistedFailures();

        // Nothing to rerun: leave the player's run (relics, deck, fast mode) untouched.
        if (failedIds.Count == 0)
        {
            onComplete?.Invoke("No failed tests to rerun.");
            return;
        }

        BeginRun(onComplete);
        foreach (var id in failedIds)
            _testQueue.Enqueue(id);
        MainFile.Logger.Info($"Rerunning {failedIds.Count} failed tests...");
        StartTest(_testQueue.Dequeue());
    }

    public static void RunAll(Action<string>? onComplete = null)
    {
        if (RelicStatsRegistry.All.Count == 0)
        {
            onComplete?.Invoke("No relics registered.");
            return;
        }

        BeginRun(onComplete);
        foreach (var (id, _) in RelicStatsRegistry.All)
            _testQueue.Enqueue(id);
        StartTest(_testQueue.Dequeue());
    }

    private static void StartTest(string relicId)
    {
        // A test that throws while being set up or started must not kill the queue: the run would
        // stop silently with the player's fast mode still forced to Instant.
        try { StartTestCore(relicId); }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[TestManager] {relicId} threw while starting: {e}");
            _activeRunner = null;
            _results.Add((relicId, new TestResult(false, $"threw while starting: {e.Message}")));
            MainFile.Logger.Info($"{relicId}: FAIL (threw while starting)");
            AdvanceQueue();
        }
    }

    private static void StartTestCore(string relicId)
    {
        var stats = RelicStatsRegistry.Get(relicId);
        if (stats == null)
        {
            _results.Add((relicId, new TestResult(false, "relic not found in registry")));
            MainFile.Logger.Info($"{relicId}: FAIL (relic not found in registry)");
            AdvanceQueue();
            return;
        }

        // Cancel any pending EndTurn or queued room transition from a previous test to prevent
        // stale timer interference
        TestHelpers.CancelPendingEndTurn();
        TestHelpers.CancelQueuedRoomTransitions();

        // Reset stats, heal player, disable god mode, clear powers, potions, card-selection
        // overrides and the permanent deck before each test, so nothing a previous test left
        // behind (a Skill Potion that needs a choice, a pushed selector) changes this one's path.
        stats.Reset();
        TestHelpers.Heal(999);
        TestHelpers.DisableGodMode();
        TestHelpers.ClearPlayerPowers();
        TestHelpers.ClearPotions();
        TestHelpers.PopCardSelector();
        ClearPlayerDeck();

        var runner = new TestRunner(relicId);
        stats.RegisterTest(runner);
        _activeRunner = runner;
        // Completion is reported by the runner itself, tied to this runner: finishing it may start
        // the next test synchronously, and a late callback from this one must not complete that one.
        runner.Completed += () => { if (_activeRunner == runner) OnTestComplete(); };
        ScheduleNextTick(runner);
        runner.Start();
    }

    // One tick chain per runner; it stops when its runner completes or is replaced.
    private static void ScheduleNextTick(TestRunner runner)
    {
        if (_activeRunner != runner || runner.IsComplete) return;
        _tickTimer = ((SceneTree)Engine.GetMainLoop()).CreateTimer(1.0);
        _tickTimer.Timeout += () => OnTick(runner);
    }

    private static void OnTick(TestRunner runner)
    {
        if (_activeRunner != runner || runner.IsComplete) return;
        runner.CheckTimeouts();
        ScheduleNextTick(runner);
    }

    public static void Signal(GameEvent gameEvent)
    {
        if (_activeRunner == null || _activeRunner.IsComplete) return;

        // Check if a deferred EndTurn can now fire (IsPlayPhase may have become true)
        TestHelpers.TryEndTurn();

        // Advance the card play queue when a card finishes resolving
        if (gameEvent == GameEvent.CardPlayed)
            TestHelpers.OnCardPlayed();

        _activeRunner.Signal(gameEvent);
    }

    public static void CheckTimeouts()
    {
        if (_activeRunner == null || _activeRunner.IsComplete) return;

        _activeRunner.CheckTimeouts();
    }

    private static string FailedTestsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SlayTheSpire2", "RelicStats_failed_tests.txt");

    private static void PersistFailedTests()
    {
        try
        {
            var failed = _results.Where(r => !r.Result.Passed).Select(r => r.RelicId).ToList();
            if (failed.Count > 0)
                File.WriteAllLines(FailedTestsPath, failed);
            else if (File.Exists(FailedTestsPath))
                File.Delete(FailedTestsPath);
        }
        catch { /* best effort */ }
    }

    private static List<string> LoadPersistedFailures()
    {
        try
        {
            if (File.Exists(FailedTestsPath))
                return File.ReadAllLines(FailedTestsPath).ToList();
        }
        catch { }
        return new List<string>();
    }

    private static void OnTestComplete()
    {
        if (_activeRunner == null) return;

        var result = _activeRunner.GetFinalResult();
        _results.Add((_activeRunner.RelicId, result));

        var status = result.Passed ? "PASS" : $"FAIL ({result.Message})";
        MainFile.Logger.Info($"{_activeRunner.RelicId}: {status}");

        _activeRunner = null;
        // Drop anything the finished test still has queued (a fight behind a transition) now,
        // rather than letting it start before the next test's setup.
        TestHelpers.CancelQueuedRoomTransitions();
        AdvanceQueue();
    }

    private static void AdvanceQueue()
    {
        if (_testQueue.Count > 0)
        {
            var nextId = _testQueue.Dequeue();
            WaitForCombatSettled(() => StartTest(nextId));
            return;
        }

        EndRun();
    }

    /// <summary>
    /// Waits until the previous combat's turn cycle has settled (IsPlayPhase or no combat).
    /// This prevents starting a new fight while async turn resolution is still in-flight.
    /// </summary>
    private static void WaitForCombatSettled(Action then, int attempts = 0)
    {
        var cm = CombatManager.Instance;
        // Never start the next test while the previous one's room transition is still running: its
        // first StartFight would queue behind it anyway, but its setup (strip, heal, clear deck)
        // would run against a room that is being torn down.
        if (TestHelpers.RoomTransitionRunningFor.TotalSeconds >= StuckTransitionSeconds)
        {
            var reason = $"room transition '{TestHelpers.RoomTransitionLabel}' still running after " +
                         $"{TestHelpers.RoomTransitionRunningFor.TotalSeconds:0}s";
            MainFile.Logger.Warn($"[TestManager] aborting the run: {reason}");
            EndRun(reason);
            return;
        }
        bool transitionDone = !TestHelpers.IsRoomTransitionPending;
        bool combatSettled = cm == null || !cm.IsInProgress || TestHelpers.Player?.PlayerCombatState?.Phase == PlayerTurnPhase.Play;
        if ((transitionDone && combatSettled) || attempts >= 100)
        {
            // Always start the next test on a fresh frame. Called straight from the previous
            // test's last signal, it would otherwise start a fight inside that hook's postfix.
            Callable.From(then).CallDeferred();
            return;
        }
        var timer = ((SceneTree)Engine.GetMainLoop()).CreateTimer(0.1);
        timer.Timeout += () => WaitForCombatSettled(then, attempts + 1);
    }

    public static void ForceTimeoutCheck()
    {
        if (_activeRunner == null || _activeRunner.IsComplete) return;
        _activeRunner.CheckTimeouts();
    }

    public static string FormatResults()
    {
        if (_results.Count == 0)
            return "No test results available. Run 'relicstats test' first.";

        var sb = new StringBuilder();
        foreach (var (relicId, result) in _results)
        {
            var status = result.Passed ? "PASS" : $"FAIL ({result.Message})";
            sb.AppendLine($"{relicId}: {status}");
        }

        var passed = _results.Count(r => r.Result.Passed);
        var failed = _results.Count - passed;
        sb.AppendLine($"---");
        sb.AppendLine($"{passed} passed, {failed} failed (of {_results.Count})");
        return sb.ToString();
    }
}
#endif
