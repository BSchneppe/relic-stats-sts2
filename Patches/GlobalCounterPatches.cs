using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using RelicStats.Core;

namespace RelicStats.Patches;

[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.IsMelted), MethodType.Setter)]
public static class WaxMeltPatch
{
    public static void Postfix(RelicModel __instance, bool value)
    {
        if (!value) return;
        if (__instance.IsCanonical) return;
        if (__instance.Owner?.RunState == null) return;
        var stats = RelicStatsRegistry.Get(__instance.Id.Entry);
        if (stats == null) return;
        RelicStatsRegistry.SetFloorMelted(__instance.Id.Entry, __instance.Owner.RunState.TotalFloor);
    }
}

[HarmonyPatch(typeof(RunManager), "InitializeNewRun")]
public static class NewRunResetPatch
{
    public static void Prefix()
    {
        var isMultiplayer = RunManager.Instance?.NetService?.Type.IsMultiplayer() == true;
        StatsPersistence.Save(isMultiplayer);
        RelicStatsRegistry.ResetAll();
    }
}

[HarmonyPatch(typeof(RunHistoryUtilities), nameof(RunHistoryUtilities.CreateRunHistoryEntry))]
public static class RunEndSavePatch
{
    public static void Prefix(SerializableRun run)
    {
        var isMultiplayer = RunManager.Instance?.NetService?.Type.IsMultiplayer() == true;
        // Save active run stats
        StatsPersistence.Save(isMultiplayer);
        // Save per-run snapshot alongside the .run history file
        StatsPersistence.SaveRunHistory(run.StartTime);
    }
}
