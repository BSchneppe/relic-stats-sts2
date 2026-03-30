using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
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

[HarmonyPatch(typeof(RunManager), "InitializeNewRun")]
public static class NewRunResetPatch
{
    public static void Prefix()
    {
        StatsPersistence.Save(isMultiplayer: RunManager.Instance?.NetService?.Type.IsMultiplayer() == true);
        RelicStatsRegistry.ResetAll();
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.AddRelicInternal))]
public static class RelicObtainedPatch
{
    public static void Postfix(RelicModel relic)
    {
        var stats = RelicStatsRegistry.Get(relic.Id.Entry);
        if (stats == null) return;
        stats.TurnWhenObtained = RelicStatsRegistry.TurnCount;
        stats.CombatWhenObtained = RelicStatsRegistry.CombatCount;
    }
}

[HarmonyPatch(typeof(RunHistoryUtilities), nameof(RunHistoryUtilities.CreateRunHistoryEntry))]
public static class RunEndSavePatch
{
    public static void Prefix()
    {
        StatsPersistence.Save(isMultiplayer: RunManager.Instance?.NetService?.Type.IsMultiplayer() == true);
    }
}
