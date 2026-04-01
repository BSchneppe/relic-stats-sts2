using System.Collections.Generic;
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
    /// CombatState.RoundNumber for any in-progress combat.
    /// </summary>
    public static (int turns, int combats) GetEffective(Player player, int startFloor, int? endFloor = null)
    {
        var (turns, combats) = GetEffective(
            player.RunState.MapPointHistory, startFloor, endFloor);

        // Add in-progress combat turns when not frozen (endFloor == null)
        if (!endFloor.HasValue)
        {
            var cm = CombatManager.Instance;
            if (cm != null && cm.IsInProgress)
            {
                var state = cm.DebugOnlyGetState();
                if (state != null)
                {
                    turns += state.RoundNumber;
                    combats++; // count the current combat
                }
            }
        }

        return (turns, combats);
    }

    /// <summary>
    /// Computes effective turns and combats from raw map point history data.
    /// Used for both active runs (via Player overload) and run history display.
    /// </summary>
    public static (int turns, int combats) GetEffective(
        IEnumerable<IEnumerable<MapPointHistoryEntry>> mapPointHistory, int startFloor, int? endFloor = null)
    {
        int turns = 0;
        int combats = 0;
        int floor = 0;

        foreach (var act in mapPointHistory)
        {
            foreach (var mapPoint in act)
            {
                floor++;
                if (floor <= startFloor) continue;
                if (endFloor.HasValue && floor > endFloor.Value) break;

                foreach (var room in mapPoint.Rooms)
                {
                    if (room.RoomType is RoomType.Monster or RoomType.Elite or RoomType.Boss)
                    {
                        turns += room.TurnsTaken;
                        combats++;
                    }
                }
            }
        }

        return (turns, combats);
    }
}
