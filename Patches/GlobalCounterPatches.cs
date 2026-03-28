using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using RelicStats.Core;

namespace RelicStats.Patches;

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterPlayerTurnStart))]
public static class TurnCounterPatch
{
    public static void Postfix()
    {
        RelicStatsRegistry.TurnCount++;
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCombatStart))]
public static class CombatCounterPatch
{
    public static void Postfix()
    {
        RelicStatsRegistry.CombatCount++;
    }
}

[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.IsMelted), MethodType.Setter)]
public static class WaxMeltPatch
{
    public static void Postfix(RelicModel __instance, bool value)
    {
        if (!value) return;
        var stats = RelicStatsRegistry.Get(__instance.Id.Entry);
        if (stats == null) return;
        stats.FrozenTurnCount = RelicStatsRegistry.TurnCount;
        stats.FrozenCombatCount = RelicStatsRegistry.CombatCount;
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.CreateForNewRun), new[] { typeof(CharacterModel), typeof(MegaCrit.Sts2.Core.Unlocks.UnlockState), typeof(ulong) })]
public static class NewRunResetPatch
{
    public static void Postfix()
    {
        RelicStatsRegistry.ResetAll();
    }
}
