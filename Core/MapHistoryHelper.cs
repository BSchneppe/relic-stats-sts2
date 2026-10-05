using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs.History;

namespace RelicStats.Core;

public static class MapHistoryHelper
{
    /// <summary>
    /// Computes effective turns and combats from map history.
    /// Counts combat rooms (Monster, Elite, Boss) between startFloor (exclusive) and
    /// endFloor (inclusive). If endFloor is null, counts to current floor and adds
    /// the in-progress combat's player turn count (PlayerCombatState.TurnNumber).
    /// </summary>
    public static (int turns, int combats) GetEffective(Player player, int startFloor, int? endFloor = null)
    {
        CombatHistoryTotals.LiveCombat? live = null;
        if (!endFloor.HasValue)
        {
            var cm = CombatManager.Instance;
            if (cm != null && cm.IsInProgress)
            {
                var state = cm.DebugOnlyGetState();
                if (state != null)
                {
                    var room = player.RunState.CurrentMapPointHistoryEntry?.Rooms.LastOrDefault();
                    bool recorded = room?.RoomType is RoomType.Monster or RoomType.Elite or RoomType.Boss;
                    live = new CombatHistoryTotals.LiveCombat(player.RunState.TotalFloor,
                        player.PlayerCombatState?.TurnNumber ?? state.RoundNumber,
                        recorded ? room!.TurnsTaken : 0, recorded);
                }
            }
        }

        return CombatHistoryTotals.Calculate(CombatVisits(player.RunState.MapPointHistory), startFloor, endFloor, live);
    }

    /// <summary>
    /// Computes effective turns and combats from raw map point history data.
    /// Used for both active runs (via Player overload) and run history display.
    /// </summary>
    public static (int turns, int combats) GetEffective(
        IEnumerable<IEnumerable<MapPointHistoryEntry>> mapPointHistory, int startFloor, int? endFloor = null)
    {
        return CombatHistoryTotals.Calculate(CombatVisits(mapPointHistory), startFloor, endFloor);
    }

    private static IEnumerable<CombatHistoryTotals.Visit> CombatVisits(
        IEnumerable<IEnumerable<MapPointHistoryEntry>> mapPointHistory)
    {
        int floor = 0;

        foreach (var act in mapPointHistory)
        {
            foreach (var mapPoint in act)
            {
                floor++;
                foreach (var room in mapPoint.Rooms)
                {
                    if (room.RoomType is RoomType.Monster or RoomType.Elite or RoomType.Boss)
                    {
                        yield return new CombatHistoryTotals.Visit(floor, room.TurnsTaken);
                    }
                }
            }
        }

    }
}
