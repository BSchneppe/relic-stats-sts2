using RelicStats.Core;
using Visit = RelicStats.Core.CombatHistoryTotals.Visit;
using Live = RelicStats.Core.CombatHistoryTotals.LiveCombat;

namespace RelicStats.Tests;

public class CombatHistoryTotalsTests
{
    [Fact]
    public void LiveFirstCombatIsAlreadyInHistory()
    {
        var result = CombatHistoryTotals.Calculate(new[] { new Visit(1, 0) }, 0,
            live: new Live(1, 3, 0, true));
        Assert.Equal((3, 1), result);
    }

    [Fact]
    public void NestedCombatKeepsEarlierRoomOnSameFloor()
    {
        var result = CombatHistoryTotals.Calculate(new[] { new Visit(2, 4), new Visit(2, 0) }, 0,
            live: new Live(2, 2, 0, true));
        Assert.Equal((6, 2), result);
    }

    [Fact]
    public void RecordedLiveTurnsAreNotCountedAgain()
    {
        var result = CombatHistoryTotals.Calculate(new[] { new Visit(1, 2) }, 0,
            live: new Live(1, 3, 2, true));
        Assert.Equal((3, 1), result);
    }

    [Fact]
    public void FightWithoutMapHistoryStillHasOneCombat()
    {
        var result = CombatHistoryTotals.Calculate(Array.Empty<Visit>(), 0,
            live: new Live(0, 2, 0, false));
        Assert.Equal((2, 1), result);
    }

    [Fact]
    public void FrozenMeltedHistoryExcludesLiveAndLaterRooms()
    {
        var result = CombatHistoryTotals.Calculate(new[] { new Visit(1, 9), new Visit(2, 3), new Visit(3, 4) },
            startFloor: 1, endFloor: 2, live: new Live(3, 6, 4, true));
        Assert.Equal((3, 1), result);
    }

    [Fact]
    public void CompletedHistoryHonorsAcquisitionFloor()
    {
        var result = CombatHistoryTotals.Calculate(new[] { new Visit(1, 5), new Visit(3, 2), new Visit(4, 4) }, 2);
        Assert.Equal((6, 2), result);
    }
}
