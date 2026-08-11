using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace RelicStats.Core;

/// <summary>
/// Resolves Harmony patch targets at runtime so one DLL works across game versions.
/// </summary>
/// <remarks>
/// The game renames hooks between versions (Fiddle's bonus draw moved from ModifyHandDrawLate to
/// ModifyHandDraw in 0.110). Harmony resolves attribute targets with DeclaredMethod, which returns
/// null for a merely inherited method, so a stale name compiles but throws at patch time and the
/// relic silently records nothing.
/// </remarks>
public static class PatchTarget
{
    /// <summary>First of <paramref name="candidateNames"/> declared on the type, else nothing.</summary>
    public static IEnumerable<MethodBase> FirstDeclared(Type type, params string[] candidateNames)
    {
        foreach (var name in candidateNames)
        {
            var method = AccessTools.DeclaredMethod(type, name);
            if (method != null)
            {
                yield return method;
                yield break;
            }
        }

        MainFile.Logger.Warn(
            $"No patch target found on {type.Name}: none of [{string.Join(", ", candidateNames)}] is declared.");
    }

    /// <summary>
    /// As <see cref="FirstDeclared"/> but silent when absent, for relics whose versions need
    /// different postfix logic and so get one patch class each. The CI audit catches a relic
    /// left with no live patch on any version.
    /// </summary>
    public static IEnumerable<MethodBase> DeclaredOrNone(Type type, string name)
    {
        var method = AccessTools.DeclaredMethod(type, name);
        if (method != null) yield return method;
    }
}
