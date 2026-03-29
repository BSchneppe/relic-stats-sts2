#if DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using Environment = System.Environment;

namespace RelicStats.Core.Testing;

public static class TestManager
{
    private static TestRunner? _activeRunner;
    private static readonly Queue<string> _testQueue = new();
    private static readonly List<(string RelicId, TestResult Result)> _results = new();
    private static Action<string>? _onAllComplete;
    public static bool IsRunning => _activeRunner != null && !_activeRunner.IsComplete;
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


    private static void PrepareTestRun()
    {
        StripPlayerRelics();
        ClearPlayerDeck();
    }

    public static void RunSingle(string relicId, Action<string>? onComplete = null)
    {
        _results.Clear();
        _testQueue.Clear();
        _onAllComplete = onComplete;
        PrepareTestRun();
        StartTest(relicId);
    }

    public static void RunFailed(Action<string>? onComplete = null)
    {
        var failedIds = _results.Where(r => !r.Result.Passed).Select(r => r.RelicId).ToList();
        if (failedIds.Count == 0)
            failedIds = LoadPersistedFailures();

        _results.Clear();
        _testQueue.Clear();
        _onAllComplete = onComplete;
        PrepareTestRun();

        foreach (var id in failedIds)
            _testQueue.Enqueue(id);

        if (_testQueue.Count > 0)
        {
            MainFile.Logger.Info($"Rerunning {failedIds.Count} failed tests...");
            StartTest(_testQueue.Dequeue());
        }
        else
            onComplete?.Invoke("No failed tests to rerun.");
    }

    public static void RunAll(Action<string>? onComplete = null)
    {
        _results.Clear();
        _testQueue.Clear();
        _onAllComplete = onComplete;
        PrepareTestRun();

        foreach (var (id, _) in RelicStatsRegistry.All)
            _testQueue.Enqueue(id);

        if (_testQueue.Count > 0)
            StartTest(_testQueue.Dequeue());
        else
            onComplete?.Invoke("No relics registered.");
    }

    private static void StartTest(string relicId)
    {
        var stats = RelicStatsRegistry.Get(relicId);
        if (stats == null)
        {
            _results.Add((relicId, new TestResult(false, "relic not found in registry")));
            MainFile.Logger.Info($"{relicId}: FAIL (relic not found in registry)");
            AdvanceQueue();
            return;
        }

        // Cancel any pending EndTurn from a previous test to prevent stale timer interference
        TestHelpers.CancelPendingEndTurn();

        // Reset stats, heal player, disable god mode, clear powers, and clear permanent deck before each test
        stats.Reset();
        TestHelpers.Heal(999);
        TestHelpers.DisableGodMode();
        TestHelpers.ClearPlayerPowers();
        ClearPlayerDeck();

        var runner = new TestRunner(relicId);
        stats.RegisterTest(runner);
        _activeRunner = runner;
        StartTimeoutTick();
        runner.Start();

        if (runner.IsComplete)
            OnTestComplete();
    }

    private static void StartTimeoutTick()
    {
        ScheduleNextTick();
    }

    private static void ScheduleNextTick()
    {
        if (_activeRunner == null || _activeRunner.IsComplete) return;
        _tickTimer = ((SceneTree)Engine.GetMainLoop()).CreateTimer(1.0);
        _tickTimer.Timeout += OnTick;
    }

    private static void OnTick()
    {
        if (_activeRunner == null || _activeRunner.IsComplete) return;
        _activeRunner.CheckTimeouts();
        if (_activeRunner.IsComplete)
        {
            OnTestComplete();
            return;
        }
        ScheduleNextTick();
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

        if (_activeRunner.IsComplete)
            OnTestComplete();
    }

    public static void CheckTimeouts()
    {
        if (_activeRunner == null || _activeRunner.IsComplete) return;

        _activeRunner.CheckTimeouts();

        if (_activeRunner.IsComplete)
            OnTestComplete();
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

        PersistFailedTests();

        var passed = _results.Count(r => r.Result.Passed);
        var failed = _results.Count - passed;
        var summary = $"{passed} passed, {failed} failed (of {_results.Count})";
        MainFile.Logger.Info($"Test run complete: {summary}");
        _onAllComplete?.Invoke(summary);
    }

    /// <summary>
    /// Waits until the previous combat's turn cycle has settled (IsPlayPhase or no combat).
    /// This prevents starting a new fight while async turn resolution is still in-flight.
    /// </summary>
    private static void WaitForCombatSettled(Action then, int attempts = 0)
    {
        var cm = CombatManager.Instance;
        if (cm == null || !cm.IsInProgress || cm.IsPlayPhase || attempts >= 25)
        {
            then();
            return;
        }
        var timer = ((SceneTree)Engine.GetMainLoop()).CreateTimer(0.1);
        timer.Timeout += () => WaitForCombatSettled(then, attempts + 1);
    }

    public static void ForceTimeoutCheck()
    {
        if (_activeRunner == null || _activeRunner.IsComplete) return;
        _activeRunner.CheckTimeouts();
        if (_activeRunner.IsComplete)
            OnTestComplete();
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
