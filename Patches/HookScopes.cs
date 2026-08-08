using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using RelicStats.Core;

namespace RelicStats.Patches;

/// <summary>
/// True while the damage hooks are computing a card's displayed number rather than real damage.
/// </summary>
/// <remarks>
/// DamageVar.UpdateCardPreview runs the global damage hooks for every card in the Hand or Play
/// pile, and once per hittable enemy in MultiCreatureTargeting mode, so a relic's
/// ModifyDamageAdditive fires constantly while the player just looks at their hand. Real damage
/// comes from CreatureCmd.Damage with CardPreviewMode.None. The relic cannot see the mode itself,
/// so it is captured here.
/// </remarks>
[HarmonyPatch]
public static class DamagePreviewScope
{
    [ThreadStatic] private static int _depth;

    public static bool IsPreview => _depth > 0;

    // Resolved by name: 0.110 added a CardPlay parameter, but there is still one overload and
    // Harmony binds previewMode by name either way.
    public static IEnumerable<MethodBase> TargetMethods() =>
        PatchTarget.FirstDeclared(typeof(Hook), nameof(Hook.ModifyDamage));

    [HarmonyPrefix]
    public static void Prefix(CardPreviewMode previewMode)
    {
        if (previewMode != CardPreviewMode.None) _depth++;
    }

    // Finalizer rather than postfix, so the depth unwinds if the hook throws.
    [HarmonyFinalizer]
    public static void Finalizer(CardPreviewMode previewMode)
    {
        if (previewMode != CardPreviewMode.None) _depth--;
    }
}

/// <summary>
/// True only during the first max-energy evaluation of an actual energy grant.
/// </summary>
/// <remarks>
/// PlayerCombatState.MaxEnergy is computed, so every UI read re-runs every relic's
/// ModifyMaxEnergy — Spiked Gauntlets reached 2538 in one run. Energy is only really granted at
/// turn start, via ResetEnergy or AddMaxEnergyToCurrent. Within that window only the first hook
/// run counts, since assigning Energy raises EnergyChanged and a handler reading MaxEnergy back
/// would re-enter. No handler does today, but that is a UI detail to not depend on.
/// </remarks>
[HarmonyPatch]
public static class EnergyGrantScope
{
    [ThreadStatic] private static int _grantDepth;
    [ThreadStatic] private static int _hookRunsThisGrant;
    [ThreadStatic] private static bool _counting;

    public static bool IsCounting => _counting;

    internal static void EnterHookRun()
    {
        if (_grantDepth <= 0) return;
        _hookRunsThisGrant++;
        _counting = _hookRunsThisGrant == 1;
    }

    internal static void ExitHookRun() => _counting = false;

    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var method in PatchTarget.FirstDeclared(typeof(PlayerCombatState), nameof(PlayerCombatState.ResetEnergy)))
            yield return method;
        foreach (var method in PatchTarget.FirstDeclared(typeof(PlayerCombatState), nameof(PlayerCombatState.AddMaxEnergyToCurrent)))
            yield return method;
    }

    [HarmonyPrefix]
    public static void Prefix()
    {
        if (_grantDepth++ == 0) _hookRunsThisGrant = 0;
    }

    [HarmonyFinalizer]
    public static void Finalizer()
    {
        if (--_grantDepth > 0) return;
        _grantDepth = 0;
        _counting = false;
    }
}

/// <summary>Marks each ModifyMaxEnergy run so EnergyGrantScope can spot re-entrant ones.</summary>
[HarmonyPatch]
public static class MaxEnergyHookScope
{
    public static IEnumerable<MethodBase> TargetMethods() =>
        PatchTarget.FirstDeclared(typeof(Hook), nameof(Hook.ModifyMaxEnergy));

    [HarmonyPrefix]
    public static void Prefix() => EnergyGrantScope.EnterHookRun();

    [HarmonyFinalizer]
    public static void Finalizer() => EnergyGrantScope.ExitHookRun();
}
