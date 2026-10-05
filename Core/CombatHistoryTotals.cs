using System;
using System.Collections.Generic;

namespace RelicStats.Core;

internal static class CombatHistoryTotals
{
    internal readonly record struct Visit(int Floor, int Turns);
    internal readonly record struct LiveCombat(int Floor, int Turns, int RecordedTurns, bool HasHistoryRoom);

    public static (int turns, int combats) Calculate(
        IEnumerable<Visit> visits, int startFloor, int? endFloor = null, LiveCombat? live = null)
    {
        int turns = 0, combats = 0;
        foreach (var visit in visits)
        {
            if (visit.Floor <= startFloor || endFloor.HasValue && visit.Floor > endFloor.Value) continue;
            turns += visit.Turns;
            combats++;
        }

        if (!endFloor.HasValue && live is { } current)
        {
            // The game appends the room before combat starts, but records TurnsTaken
            // later. Add its live turns, not another combat or a second copy of turns.
            bool included = current.HasHistoryRoom && current.Floor > startFloor;
            turns += Math.Max(0, current.Turns - (included ? current.RecordedTurns : 0));
            if (!included) combats++;
        }
        return (turns, combats);
    }
}
