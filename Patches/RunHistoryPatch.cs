using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.RunHistoryScreen;
using MegaCrit.Sts2.Core.Runs;
using RelicStats.Core;

namespace RelicStats.Patches;

[HarmonyPatch(typeof(NRunHistory), "DisplayRun")]
public static class RunHistoryDisplayPatch
{
    public static void Prefix(RunHistory history)
    {
        RunHistoryContext.Current = history;
    }
}

[HarmonyPatch(typeof(NRunHistory), "OnSubmenuHidden")]
public static class RunHistoryClearPatch
{
    public static void Postfix()
    {
        RunHistoryContext.Current = null;
    }
}
