using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using RelicStats.Core;

namespace RelicStats.Patches;

[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.HoverTip), MethodType.Getter)]
public static class HoverTipPatch
{
    public static void Postfix(RelicModel __instance, ref HoverTip __result)
    {
        if (__instance.IsMelted) return;

        var stats = RelicStatsRegistry.Get(__instance.Id.Entry);
        if (stats == null) return;

        var description = __result.Description;
        var floorText = $"Obtained on floor {__instance.FloorAddedToDeck}";
        var statsText = stats.GetDescription(RelicStatsRegistry.TurnCount, RelicStatsRegistry.CombatCount);
        var appendText = $"\n[color=gray]{floorText}[/color]\n{statsText}";

        object boxed = __result;
        AccessTools.Property(typeof(HoverTip), nameof(HoverTip.Description))
            .SetValue(boxed, description + appendText);
        __result = (HoverTip)boxed;
    }
}
