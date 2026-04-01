using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using RelicStats.Core;

namespace RelicStats.Patches;

[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.HoverTips), MethodType.Getter)]
public static class HoverTipsPatch
{
    public static void Postfix(RelicModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (__instance.IsCanonical) return;
        if (__instance.Owner == null) return;

        var stats = RelicStatsRegistry.Get(__instance.Id.Entry);
        if (stats == null) return;

        // Distinguish active run vs history display:
        // History players are created via Player.CreateForNewRun and have NullRunState.
        var isActiveRun = __instance.Owner.RunState is not NullRunState;
        var relicId = __instance.Id.Entry;
        var floorText = $"Floor obtained: {__instance.FloorAddedToDeck}";
        string statsText;
        if (isActiveRun)
        {
            var floorMelted = RelicStatsRegistry.GetFloorMelted(relicId);
            var (turns, combats) = MapHistoryHelper.GetEffective(
                __instance.Owner, __instance.FloorAddedToDeck, floorMelted);
            statsText = stats.GetDescription(turns, combats);
        }
        else
        {
            // History relic — derive averages from RunHistory.MapPointHistory
            var history = RunHistoryContext.Current;
            if (history == null) return;

            var savedDesc = StatsPersistence.GetRunHistoryDescription(
                relicId, history.StartTime, history.MapPointHistory, __instance.FloorAddedToDeck);
            if (savedDesc == null) return;
            statsText = savedDesc;
        }
        var description = $"{floorText}\n{statsText}";

        // Construct HoverTip with raw strings via reflection (Title/Description have private setters)
        var tip = new HoverTip();
        object boxed = tip;
        AccessTools.Property(typeof(HoverTip), nameof(HoverTip.Title)).SetValue(boxed, "Stats");
        AccessTools.Property(typeof(HoverTip), nameof(HoverTip.Description)).SetValue(boxed, description);
        tip = (HoverTip)boxed;

        var list = __result.ToList();
        list.Add(tip);
        __result = list;
    }
}
