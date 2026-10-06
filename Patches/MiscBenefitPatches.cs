using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Models.Relics;
using RelicStats.Relics;

namespace RelicStats.Patches;

[HarmonyPatch(typeof(DigRestSiteOption), nameof(DigRestSiteOption.OnSelect))]
internal static class ShovelDigCompletedPatch
{
    public static void Postfix(DigRestSiteOption __instance, ref Task<bool> __result)
    {
        if (ShovelStats.OfferedOptions.TryGetValue(__instance, out var shovel))
            __result = Observe(__result, shovel, __instance);
    }

    private static async Task<bool> Observe(Task<bool> task, Shovel shovel, RestSiteOption option)
    {
        bool completed = await task;
        if (completed && ShovelStats.OfferedOptions.TryGetValue(option, out _)) {
            ShovelStats.OfferedOptions.Remove(option);
            ShovelStats.Track(shovel, stats => ((ShovelStats)stats).RecordedAmount++);
        }
        return completed;
    }
}

[HarmonyPatch(typeof(RuinedHelmet), nameof(RuinedHelmet.TryModifyPowerAmountReceived))]
internal static class RuinedHelmetExtraStrengthPatch
{
    public static void Postfix(RuinedHelmet __instance, decimal amount, decimal modifiedAmount, bool __result)
    {
        if (!__result) return;
        RuinedHelmetStats.Pending.GetValue(__instance, _ => new RuinedHelmetStats.PendingStrength()).Amount =
            modifiedAmount - amount;
    }
}
