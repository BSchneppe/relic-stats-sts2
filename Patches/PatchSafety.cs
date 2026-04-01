using System;

namespace RelicStats.Patches;

/// <summary>
/// Global Harmony finalizer that catches and logs exceptions from all RelicStats patches,
/// preventing mod bugs from soft-locking the game.
/// </summary>
public static class PatchSafety
{
    public static Exception? Finalizer(Exception __exception)
    {
        if (__exception != null)
        {
            MainFile.Logger.Warn($"[RelicStats] Patch error (swallowed): {__exception}");
        }
        return null;
    }
}
