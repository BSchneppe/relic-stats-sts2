using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Managers;
using RelicStats.Core;

namespace RelicStats.Patches;

/// <summary>
/// RunSaveManager.SaveRun is async, so Harmony prefix may not fire reliably.
/// Instead, patch RunManager.ToSave which is synchronous and called during every save.
/// </summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.ToSave))]
public static class SaveRunPatch
{
    public static void Prefix()
    {
        var isMultiplayer = RunManager.Instance?.NetService?.Type.IsMultiplayer() == true;
        StatsPersistence.Save(isMultiplayer);
    }
}

[HarmonyPatch(typeof(RunSaveManager), nameof(RunSaveManager.LoadRunSave))]
public static class LoadRunSavePatch
{
    public static void Postfix()
    {
        StatsPersistence.Load(isMultiplayer: false);
    }
}

[HarmonyPatch(typeof(RunSaveManager), nameof(RunSaveManager.LoadMultiplayerRunSave))]
public static class LoadMultiplayerRunSavePatch
{
    public static void Postfix()
    {
        StatsPersistence.Load(isMultiplayer: true);
    }
}

