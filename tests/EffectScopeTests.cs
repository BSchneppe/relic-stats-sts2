using RelicStats.Core;

namespace RelicStats.Tests;

public class EffectScopeTests
{
    [Fact]
    public async Task CountsSequentialDirectCommandsButNotNestedCommands()
    {
        var owner = new object();
        var values = new List<int>();
        using var source = EffectScope<int>.Begin(owner, values.Add);
        async Task<int> Command()
        {
            await Task.Yield();
            Assert.Null(EffectScope<int>.EnterCommand(owner));
            return 3;
        }

        var first = EffectScope<int>.EnterCommand(owner)!;
        var firstTask = first.Observe(Command());
        first.Dispose();
        Assert.Equal(3, await firstTask);
        var second = EffectScope<int>.EnterCommand(owner)!;
        var secondTask = second.Observe(Task.FromResult(2));
        second.Dispose();
        Assert.Equal(2, await secondTask);
        Assert.Equal(new[] { 3, 2 }, values);
    }

    [Fact]
    public async Task CapturedContinuationCountsAfterCallerIsRestored()
    {
        var owner = new object();
        var values = new List<int>();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Effect()
        {
            await gate.Task;
            using var command = EffectScope<int>.EnterCommand(owner)!;
            await command.Observe(Task.FromResult(7));
        }

        var source = EffectScope<int>.Begin(owner, values.Add);
        var effect = Effect();
        source.Dispose();
        Assert.Null(EffectScope<int>.CurrentOwner);
        gate.SetResult();
        await effect;
        Assert.Equal(new[] { 7 }, values);
        Assert.Null(EffectScope<int>.CurrentOwner);
    }

    [Fact]
    public async Task FailedAndCancelledCommandsDoNotFabricateResults()
    {
        var owner = new object();
        var values = new List<int>();
        using var source = EffectScope<int>.Begin(owner, values.Add);
        var failed = EffectScope<int>.EnterCommand(owner)!;
        var failedTask = failed.Observe(Task.FromException<int>(new InvalidOperationException()));
        failed.Dispose();
        await Assert.ThrowsAsync<InvalidOperationException>(() => failedTask);
        var cancelled = EffectScope<int>.EnterCommand(owner)!;
        var cancelledTask = cancelled.Observe(Task.FromCanceled<int>(new CancellationToken(true)));
        cancelled.Dispose();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledTask);
        Assert.Empty(values);
        Assert.Same(owner, EffectScope<int>.CurrentOwner);
    }

    [Fact]
    public async Task FirstOnlyAndOwnerIdentityLimitAttribution()
    {
        var owner = new object();
        var values = new List<int>();
        using var source = EffectScope<int>.Begin(owner, values.Add, firstOnly: true);
        Assert.Null(EffectScope<int>.EnterCommand(new object()));
        var command = EffectScope<int>.EnterCommand(owner)!;
        var task = command.Observe(Task.FromResult(1));
        command.Dispose();
        await task;
        Assert.Null(EffectScope<int>.EnterCommand(owner));
        Assert.Equal(new[] { 1 }, values);
    }

    [Fact]
    public async Task NestedSourcesRestoreOuterAttribution()
    {
        var owner = new object();
        var outerValues = new List<int>();
        var innerValues = new List<int>();
        using var outer = EffectScope<int>.Begin(owner, outerValues.Add);
        using (EffectScope<int>.Begin(owner, innerValues.Add))
        {
            var command = EffectScope<int>.EnterCommand(owner)!;
            var task = command.Observe(Task.FromResult(2));
            command.Dispose();
            await task;
        }
        var next = EffectScope<int>.EnterCommand(owner)!;
        var nextTask = next.Observe(Task.FromResult(3));
        next.Dispose();
        await nextTask;
        Assert.Equal(new[] { 2 }, innerValues);
        Assert.Equal(new[] { 3 }, outerValues);
    }
}
