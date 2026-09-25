#if DEBUG
using HarmonyLib;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;
using RelicStats.Core.Testing;

namespace RelicStats.Patches;

// A test run forces the in-memory fast mode to Instant and restores it when the run ends. The game
// writes prefs to disk on its own schedule (quitting, closing the settings or bestiary screen), and
// a write during a run would persist Instant as the player's setting. While a run holds the
// setting, write the player's real value and put Instant back afterwards.
[HarmonyPatch(typeof(SaveManager), nameof(SaveManager.SavePrefsFile))]
public static class TestPrefsSavePatch
{
    public static void Prefix(SaveManager __instance, out bool __state)
    {
        __state = false;
        var saved = TestManager.SavedFastMode;
        var prefs = __instance.PrefsSave;
        if (saved == null || prefs == null) return;
        prefs.FastMode = saved.Value;
        __state = true;
    }

    public static void Postfix(SaveManager __instance, bool __state)
    {
        if (!__state || TestManager.SavedFastMode == null) return;
        __instance.PrefsSave.FastMode = FastModeType.Instant;
    }
}
#endif
