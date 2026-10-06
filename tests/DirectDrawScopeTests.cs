using RelicStats.Core;

namespace RelicStats.Tests;

public class DirectDrawScopeTests
{
    [Fact]
    public async Task DirectDrawExcludesNestedDraws()
    {
        var player = new object();
        int credited = 0;
        using var scope = DirectDrawScope.Begin(player, count => credited += count);
        var direct = DirectDrawScope.Claim(player)!;
        Assert.Null(DirectDrawScope.Claim(player));
        var cards = await direct.CountWhenDone(Task.FromResult<IEnumerable<int>>(new[] { 1 }));
        Assert.Single(cards);
        Assert.Equal(1, credited);
    }

    [Fact]
    public async Task ScopeSurvivesAnAwaitAfterCallerRestoresItsContext()
    {
        var player = new object();
        int credited = 0;
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var scope = DirectDrawScope.Begin(player, count => credited += count);
        async Task DrawLater()
        {
            await resume.Task;
            var direct = DirectDrawScope.Claim(player)!;
            Assert.NotNull(direct);
            await direct.CountWhenDone(Task.FromResult<IEnumerable<int>>(new[] { 1, 2 }));
        }
        var pending = DrawLater();
        scope.Dispose();
        Assert.Null(DirectDrawScope.Claim(player));
        resume.SetResult();
        await pending;
        Assert.Equal(2, credited);
    }

    [Fact]
    public async Task NestedRelicGetsItsOwnCreditAndRestoresOuterScope()
    {
        var player = new object();
        int outerCount = 0, innerCount = 0;
        using var outer = DirectDrawScope.Begin(player, count => outerCount += count);
        var direct = DirectDrawScope.Claim(player)!;
        using (DirectDrawScope.Begin(player, count => innerCount += count))
        {
            var nested = DirectDrawScope.Claim(player)!;
            await nested.CountWhenDone(Task.FromResult<IEnumerable<int>>(new[] { 2, 3 }));
        }
        Assert.Null(DirectDrawScope.Claim(player));
        await direct.CountWhenDone(Task.FromResult<IEnumerable<int>>(new[] { 1 }));
        Assert.Equal(1, outerCount);
        Assert.Equal(2, innerCount);
    }

    [Fact]
    public void OtherPlayersDrawsCannotClaimTheScope()
    {
        var player = new object();
        using var scope = DirectDrawScope.Begin(player, _ => { });
        Assert.Null(DirectDrawScope.Claim(new object()));
        Assert.Same(scope, DirectDrawScope.Claim(player));
    }

    [Fact]
    public async Task EmptyDrawCountsNothing()
    {
        using var scope = DirectDrawScope.Begin(new object(), _ => Assert.Fail("empty draw credited"));
        Assert.Empty(await scope.CountWhenDone(Task.FromResult<IEnumerable<int>>(Array.Empty<int>())));
    }

    [Fact]
    public async Task FaultedDrawPreservesFailureWithoutCredit()
    {
        using var scope = DirectDrawScope.Begin(new object(), _ => Assert.Fail("failed draw credited"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.CountWhenDone(
            Task.FromException<IEnumerable<int>>(new InvalidOperationException("draw failed"))));
    }

    [Fact]
    public void RepeatedCleanupDoesNotClobberANewerScope()
    {
        var player = new object();
        var old = DirectDrawScope.Begin(player, _ => { });
        old.Dispose();
        using var current = DirectDrawScope.Begin(player, _ => { });
        old.Dispose();
        Assert.Same(current, DirectDrawScope.Claim(player));
    }
}
