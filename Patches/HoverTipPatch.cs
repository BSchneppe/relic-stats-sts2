using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using RelicStats.Core;

namespace RelicStats.Patches;

[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.HoverTips), MethodType.Getter)]
public static class HoverTipsPatch
{
    public static void Postfix(RelicModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (__instance.IsCanonical) return;
        if (__instance.IsMelted) return;
        if (__instance.Owner == null) return;

        var stats = RelicStatsRegistry.Get(__instance.Id.Entry);
        if (stats == null) return;

        // Check if relic is in owner's inventory (active run) vs history display
        var isOwned = __instance.Owner.Relics.Any(r => r.Id == __instance.Id);
        var floorText = $"Floor obtained: {__instance.FloorAddedToDeck}";
        string statsText;
        if (isOwned)
        {
            statsText = stats.GetDescription(RelicStatsRegistry.TurnCount, RelicStatsRegistry.CombatCount);
        }
        else
        {
            // History relic — try to show saved stats
            var saved = StatsPersistence.GetSavedDescription(__instance.Id.Entry);
            if (saved == null) return;
            statsText = saved;
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
