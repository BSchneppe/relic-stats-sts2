using HarmonyLib;
using MegaCrit.Sts2.Core.Saves.Managers;
using RelicStats.Core;

namespace RelicStats.Patches;

[HarmonyPatch(typeof(RunSaveManager), nameof(RunSaveManager.SaveRun))]
public static class SaveRunPatch
{
    public static void Prefix()
    {
        StatsPersistence.Save(isMultiplayer: false);
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
