using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using RelicStats.Core;

namespace RelicStats.Patches;

[HarmonyPatch]
internal static class DirectRelicDamagePatch
{
    // Patch only the canonical decimal/multiple-target implementation. Its CardPlay parameter
    // was added in later releases; wrapper overloads must not report each hit twice.
    public static IEnumerable<MethodBase> TargetMethods() => typeof(CreatureCmd)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Where(method => method.Name == nameof(CreatureCmd.Damage)
            && method.ReturnType == typeof(Task<IEnumerable<DamageResult>>)
            && method.GetParameters().Length >= 6
            && method.GetParameters()[1].ParameterType == typeof(IEnumerable<Creature>)
            && method.GetParameters()[2].ParameterType == typeof(decimal))
        .OrderByDescending(method => method.GetParameters().Length).Take(1);

    public static void Prefix(out EffectScope<IEnumerable<DamageResult>>.Lease? __state)
    {
        var owner = EffectScope<IEnumerable<DamageResult>>.CurrentOwner;
        __state = owner == null ? null : EffectScope<IEnumerable<DamageResult>>.EnterCommand(owner);
    }

    public static void Postfix(ref Task<IEnumerable<DamageResult>> __result,
        EffectScope<IEnumerable<DamageResult>>.Lease? __state)
    {
        if (__state != null) __result = __state.Observe(__result);
    }

    public static void Finalizer(EffectScope<IEnumerable<DamageResult>>.Lease? __state) => __state?.Dispose();
}
