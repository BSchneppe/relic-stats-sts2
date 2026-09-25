#if DEBUG
using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Combat;

namespace RelicStats.Core.Testing;

public class TestRunner
{
    private readonly List<TestStep> _steps = new();
    private Action? _cleanup;
    private int _currentStep;
    private DateTime _waitStarted;
    private readonly List<TestResult> _results = new();
    private bool _failed;

    private DateTime _testStarted;
    private const int GlobalTimeoutMs = 60000;
    private bool _combatWasActive;

    // Events that fired while a Do step was executing. A helper that completes its game command
    // synchronously (an exhaust with no animation, a shuffle inside a draw) raises the hook, and
    // with it the signal, before the runner has reached the next WaitFor. Without this buffer that
    // signal was simply dropped and the WaitFor timed out, which made such tests pass or fail on
    // whether the game happened to yield first. The buffer is cleared when the next Do starts, so
    // it can only satisfy the WaitFor that directly follows the step that raised it.
    // Counted, not a set: one step can legitimately raise the same event several times (a deck add
    // that also clones the card fires CardChangedPiles twice), and each can satisfy one WaitFor.
    private readonly Dictionary<GameEvent, int> _firedDuringStep = new();

    public string RelicId { get; }
    public bool IsComplete { get; private set; }

    /// <summary>
    /// Raised once, when the test finishes by any path: a signal, a timeout tick, or a WaitUntil
    /// poll timer. The manager must not rely on noticing IsComplete after its own calls, because a
    /// WaitUntil step resumes the runner from a timer the manager never sees.
    /// </summary>
    public event Action? Completed;
    public bool IsWaiting => !IsComplete && _currentStep < _steps.Count && _steps[_currentStep] is WaitForStep;

    public TestRunner(string relicId)
    {
        RelicId = relicId;
    }

    public void Do(string label, Action action)
    {
        _steps.Add(new DoStep(label, action));
    }

    public void WaitFor(GameEvent gameEvent, int timeoutMs = 5000)
    {
        _steps.Add(new WaitForStep(gameEvent, timeoutMs));
    }

    public void Assert(string label, Func<TestResult> check)
    {
        _steps.Add(new AssertStep(label, check));
    }

    /// <summary>
    /// Waits until <paramref name="condition"/> holds, polling every 50ms. For state that settles
    /// after a hook with no matching event (e.g. Doom's AfterDiedToDoom runs after CombatVictory).
    /// Times out as a failure naming <paramref name="label"/>; it never passes on its own.
    /// </summary>
    public void WaitUntil(string label, Func<bool> condition, int timeoutMs = 5000)
    {
        _steps.Add(new WaitUntilStep(label, condition, timeoutMs));
    }

    public void Cleanup(Action action)
    {
        _cleanup = action;
    }

    public void Start()
    {
        _currentStep = 0;
        _failed = false;
        IsComplete = false;
        _results.Clear();
        _firedDuringStep.Clear();
        _testStarted = DateTime.UtcNow;
        Advance();
    }

    public void Signal(GameEvent gameEvent)
    {
        if (IsComplete || _currentStep >= _steps.Count) return;
        if (_steps[_currentStep] is not WaitForStep wait)
        {
            // Inside a Do or Assert step: remember it for the WaitFor that follows.
            _firedDuringStep[gameEvent] = _firedDuringStep.GetValueOrDefault(gameEvent) + 1;
            return;
        }

        // Track that combat is active once we see any combat event
        if (!_combatWasActive)
        {
            var cm = CombatManager.Instance;
            if (cm != null && cm.IsInProgress)
                _combatWasActive = true;
        }

        if (wait.Event == gameEvent)
        {
            MainFile.Logger.Info($"[Test:{RelicId}] WaitFor({wait.Event}) satisfied");
            _currentStep++;
            Advance();
        }
        else
        {
            CheckTimeout(wait);
        }
    }

    public void CheckTimeouts()
    {
        if (IsComplete || _currentStep >= _steps.Count) return;

        // Global timeout — if the entire test has been running too long, abort
        var totalElapsed = DateTime.UtcNow - _testStarted;
        if (totalElapsed.TotalMilliseconds >= GlobalTimeoutMs)
        {
            var stepLabel = _steps[_currentStep] switch
            {
                WaitForStep w => $"WaitFor({w.Event})",
                DoStep d => $"Do(\"{d.Label}\")",
                AssertStep a => $"Assert(\"{a.Label}\")",
                _ => "unknown"
            };
            _results.Add(new TestResult(false, $"global timeout after {GlobalTimeoutMs}ms at step: {stepLabel}"));
            _failed = true;
            RunCleanup();
            return;
        }

        if (_steps[_currentStep] is WaitForStep wait)
            CheckTimeout(wait);
    }

    private void CheckTimeout(WaitForStep wait)
    {
        // If combat ended while waiting for a combat event, fail immediately
        // Only check after combat was confirmed active (CombatStart was seen)
        var cm = CombatManager.Instance;
        // Between a room transition's start and the new combat's start there is legitimately no
        // combat in progress (the next fight is queued behind a rest site's fade-in); that is not
        // "combat ended".
        if (_combatWasActive && cm != null && !cm.IsInProgress && !TestHelpers.IsRoomTransitionPending
            && wait.Event is GameEvent.CardPlayed
            or GameEvent.TurnEnd or GameEvent.AfterTurnEnd or GameEvent.PlayerTurnStart
            or GameEvent.SideTurnStart
            or GameEvent.DamageReceived or GameEvent.CardExhausted or GameEvent.CardDiscarded
            or GameEvent.Shuffle or GameEvent.PotionUsed)
        {
            _results.Add(new TestResult(false, $"combat ended while waiting for {wait.Event}"));
            _failed = true;
            RunCleanup();
            return;
        }

        var elapsed = DateTime.UtcNow - _waitStarted;
        if (elapsed.TotalMilliseconds >= wait.TimeoutMs)
        {
            _results.Add(new TestResult(false, $"timed out at step: WaitFor({wait.Event}) after {wait.TimeoutMs}ms"));
            _failed = true;
            RunCleanup();
        }
    }

    private void Advance()
    {
        while (_currentStep < _steps.Count)
        {
            var step = _steps[_currentStep];

            switch (step)
            {
                case DoStep doStep:
                    _firedDuringStep.Clear();
                    try
                    {
                        MainFile.Logger.Info($"[Test:{RelicId}] Do(\"{doStep.Label}\")");
                        doStep.Action();
                    }
                    catch (Exception ex)
                    {
                        MainFile.Logger.Info($"[Test:{RelicId}] Do(\"{doStep.Label}\") THREW: {ex.Message}");
                        _results.Add(new TestResult(false, $"Do(\"{doStep.Label}\") threw: {ex.Message}"));
                        _failed = true;
                        RunCleanup();
                        return;
                    }
                    _currentStep++;
                    break;

                case WaitForStep waitStep:
                    if (_firedDuringStep.TryGetValue(waitStep.Event, out var pending) && pending > 0)
                    {
                        _firedDuringStep[waitStep.Event] = pending - 1;
                        MainFile.Logger.Info($"[Test:{RelicId}] WaitFor({waitStep.Event}) satisfied (fired during the previous step)");
                        _currentStep++;
                        break;
                    }
                    MainFile.Logger.Info($"[Test:{RelicId}] WaitFor({waitStep.Event})...");
                    _waitStarted = DateTime.UtcNow;
                    return;

                case WaitUntilStep untilStep:
                    bool met;
                    try { met = untilStep.Condition(); }
                    catch (Exception ex)
                    {
                        _results.Add(new TestResult(false, $"WaitUntil(\"{untilStep.Label}\") threw: {ex.Message}"));
                        _failed = true;
                        break;
                    }
                    if (met)
                    {
                        MainFile.Logger.Info($"[Test:{RelicId}] WaitUntil(\"{untilStep.Label}\") satisfied");
                        _currentStep++;
                        break;
                    }
                    if (untilStep.StartedAt == null)
                    {
                        untilStep.StartedAt = DateTime.UtcNow;
                        MainFile.Logger.Info($"[Test:{RelicId}] WaitUntil(\"{untilStep.Label}\")...");
                    }
                    if ((DateTime.UtcNow - untilStep.StartedAt.Value).TotalMilliseconds >= untilStep.TimeoutMs)
                    {
                        _results.Add(new TestResult(false, $"timed out at step: WaitUntil(\"{untilStep.Label}\") after {untilStep.TimeoutMs}ms"));
                        _failed = true;
                        break;
                    }
                    var poll = ((Godot.SceneTree)Godot.Engine.GetMainLoop()).CreateTimer(0.05);
                    poll.Timeout += () => { if (!IsComplete) Advance(); };
                    return;

                case AssertStep assertStep:
                    try
                    {
                        var result = assertStep.Check();
                        MainFile.Logger.Info($"[Test:{RelicId}] Assert(\"{assertStep.Label}\"): {(result.Passed ? "PASS" : "FAIL")} {result.Message}");
                        _results.Add(result with { Message = result.Message ?? assertStep.Label });
                        if (!result.Passed) _failed = true;
                    }
                    catch (Exception ex)
                    {
                        _results.Add(new TestResult(false, $"Assert(\"{assertStep.Label}\") threw: {ex.Message}"));
                        _failed = true;
                    }
                    _currentStep++;
                    break;
            }

            if (_failed)
            {
                RunCleanup();
                return;
            }
        }

        RunCleanup();
    }

    private void RunCleanup()
    {
        if (_cleanup != null)
        {
            try
            {
                _cleanup();
            }
            catch (Exception ex)
            {
                _results.Add(new TestResult(false, $"Cleanup threw: {ex.Message}"));
            }
        }
        if (IsComplete) return;
        IsComplete = true;
        Completed?.Invoke();
    }

    public TestResult GetFinalResult()
    {
        if (_results.Count == 0)
            return new TestResult(true);

        foreach (var r in _results)
        {
            if (!r.Passed)
                return new TestResult(false, r.Message);
        }
        return new TestResult(true);
    }

    private abstract record TestStep;
    private record DoStep(string Label, Action Action) : TestStep;
    private record WaitForStep(GameEvent Event, int TimeoutMs) : TestStep;
    private record AssertStep(string Label, Func<TestResult> Check) : TestStep;
    private record WaitUntilStep(string Label, Func<bool> Condition, int TimeoutMs) : TestStep
    {
        public DateTime? StartedAt { get; set; }
    }
}
#endif
