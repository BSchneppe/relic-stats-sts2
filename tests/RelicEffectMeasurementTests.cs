using RelicStats.Core;

namespace RelicStats.Tests;

public class RelicEffectMeasurementTests
{
    [Fact]
    public async Task ThreePuzzleDrawCommandsCountEachDirectCardButNotNestedDraws()
    {
        var owner = new object();
        int total = 0;
        using var source = DirectDrawScope.Begin(owner, count => total += count, firstOnly: false);
        for (int i = 0; i < 3; i++)
        {
            var command = DirectDrawScope.EnterCommand(owner)!;
            Assert.NotNull(command);
            Assert.Null(DirectDrawScope.EnterCommand(owner));
            var pending = DirectDrawScope.CountCommand(Task.FromResult<IEnumerable<int>>(new[] { i }), command);
            command.Dispose();
            Assert.Single(await pending);
        }
        Assert.Equal(3, total);
    }

    [Fact]
    public async Task PreventedPuzzleDrawsRecordNoCardsEvenAcrossAllThreeCalls()
    {
        var owner = new object();
        int total = 0;
        using var source = DirectDrawScope.Begin(owner, count => total += count, firstOnly: false);
        for (int i = 0; i < 3; i++)
        {
            var command = DirectDrawScope.EnterCommand(owner)!;
            var pending = DirectDrawScope.CountCommand(Task.FromResult<IEnumerable<int>>(Array.Empty<int>()), command);
            command.Dispose();
            Assert.Empty(await pending);
        }
        Assert.Equal(0, total);
    }

    [Fact]
    public async Task ActualAutoplaySurvivesAsyncReturnAndCountsNoNestedOrRepeatedPlays()
    {
        var owner = new object(); var card = new object(); var nested = new object();
        int total = 0;
        var source = AutoPlayScope.Begin(owner, () => total++);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Play()
        {
            var command = AutoPlayScope.EnterCommand(owner, card)!;
            async Task Finish()
            {
                await resume.Task;
                using (AutoPlayScope.EnterCommand(owner, nested)) AutoPlayScope.Finished(nested, true);
                AutoPlayScope.Finished(card, false);
                AutoPlayScope.Finished(card, true);
                AutoPlayScope.Finished(card, true);
            }
            var pending = Finish(); command.Dispose(); await pending;
        }
        var play = Play(); source.Dispose(); resume.SetResult(); await play;
        Assert.Equal(1, total);
    }

    [Fact]
    public void BlockedAutoplayCannotBecomeACompletedPlay()
    {
        var owner = new object(); var card = new object(); int total = 0;
        using var source = AutoPlayScope.Begin(owner, () => total++);
        using (AutoPlayScope.EnterCommand(owner, card)) { }
        AutoPlayScope.Finished(card, true);
        Assert.Equal(0, total);
    }

    [Fact]
    public void ChipCountsOnlySelectedActualDiscardsAndIgnoresNestedCommands()
    {
        var owner = new object(); var selected = new object(); var unrelated = new object(); int total = 0;
        using var source = ChipDiscardScope.Begin(owner, () => total++);
        using (ChipDiscardScope.EnterCommand(owner, new[] { selected }))
        {
            using (ChipDiscardScope.EnterCommand(owner, new[] { unrelated })) ChipDiscardScope.Discarded(unrelated);
            ChipDiscardScope.Discarded(unrelated);
            ChipDiscardScope.Discarded(selected);
            ChipDiscardScope.Discarded(selected);
        }
        Assert.Equal(1, total);
    }

    [Fact]
    public async Task MendPreviewDoesNotCountAndRecipientFlowsIntoActualHeal()
    {
        var recipient = new object(); int total = 0;
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var scope = RestHealScope.Begin();
        RestHealScope.Prepare(recipient, count => total += count);
        async Task Heal()
        {
            await resume.Task;
            var actual = RestHealScope.Take(recipient)!;
            Assert.NotNull(actual);
            actual(RelicMeasurementMath.ExtraHealing(20, 6, 20));
            Assert.Null(RestHealScope.Take(recipient));
        }
        var pending = Heal(); scope.Dispose();
        Assert.Null(RestHealScope.Take(recipient)); Assert.Equal(0, total);
        resume.SetResult(); await pending;
        Assert.Equal(14, total);
    }

    [Theory]
    [InlineData(2, 2, 0)]
    [InlineData(2, 0, 2)]
    [InlineData(0, 3, -3)]
    public void SneckoUsesUpgradedBaseRatherThanCanonicalOrGlobalCost(int upgradedBase, int roll, int expected)
    {
        Assert.True(RelicMeasurementMath.TryRollDiscount(false, upgradedBase, roll, out int discount));
        Assert.Equal(expected, discount);
    }

    [Fact]
    public void SneckoExcludesXAndNonRollCosts()
    {
        Assert.False(RelicMeasurementMath.TryRollDiscount(true, 0, 0, out _));
        Assert.False(RelicMeasurementMath.TryRollDiscount(false, 2, 4, out _));
    }

    [Theory]
    [InlineData(15, 30, 10, 0)]
    [InlineData(45, 30, 35, 5)]
    [InlineData(80, 80, 70, 0)]
    public void PillowCountsOnlyHealingThatFitsAndSurvivesOtherModifiers(int actual, int baseline, int missing, int expected) =>
        Assert.Equal(expected, RelicMeasurementMath.ExtraHealing(actual, baseline, missing));
}
