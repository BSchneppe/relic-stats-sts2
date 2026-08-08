using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using RelicStats.Core;

namespace RelicStats;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "RelicStats";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        RelicStatsRegistry.DiscoverAndRegister();
        var harmony = new Harmony(ModId);
        PatchAllResilient(harmony);
    }

    private static void PatchAllResilient(Harmony harmony)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var patchTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttributes<HarmonyPatch>().Any());

        var succeeded = 0;
        var failed = 0;

        foreach (var type in patchTypes)
        {
            // Version-specific patch classes resolve to nothing on the versions they do not apply
            // to; Harmony treats an empty TargetMethods as an error, so skip them here instead.
            if (ResolvesToNoTarget(type)) continue;

            try
            {
                harmony.CreateClassProcessor(type).Patch();
                succeeded++;
            }
            catch (Exception e)
            {
                failed++;
                Logger.Warn($"Failed to patch {type.Name}: {e.Message}");
            }
        }

        Logger.Info($"Patched {succeeded} targets ({failed} failed)");
        ReportUntrackedRelics(harmony);

        // Global finalizer swallows exceptions from patched methods. The scope guards are excluded:
        // it would swallow the *original* method's exceptions too, and those targets are core damage
        // and energy code, where a masked failure silently zeroes a player's damage.
        var guardedTargets = Patches.DamagePreviewScope.TargetMethods()
            .Concat(Patches.EnergyGrantScope.TargetMethods())
            .Concat(Patches.MaxEnergyHookScope.TargetMethods())
            .ToHashSet();

        var finalizerMethod = typeof(Patches.PatchSafety).GetMethod(nameof(Patches.PatchSafety.Finalizer));
        foreach (var method in harmony.GetPatchedMethods())
        {
            if (guardedTargets.Contains(method)) continue;
            harmony.Patch(method, finalizer: new HarmonyMethod(finalizerMethod));
        }
    }

    private static bool ResolvesToNoTarget(Type type)
    {
        var targetMethods = type.GetMethod("TargetMethods",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (targetMethods == null) return false;

        try
        {
            return targetMethods.Invoke(null, null) is IEnumerable<MethodBase> targets && !targets.Any();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Logs any relic left with no live patch, which means its stats are dead rather than zero.
    /// Every stats class patches at least one method on its own relic type.
    /// </summary>
    private static void ReportUntrackedRelics(Harmony harmony)
    {
        var patchedRelics = harmony.GetPatchedMethods()
            .Select(method => method.DeclaringType)
            .Where(type => type != null && typeof(RelicModel).IsAssignableFrom(type))
            .Select(type => RelicIdHelper.Slugify(type!.Name))
            .ToHashSet();

        // Registry entries that are not real relics (the DEBUG-only harness tests) never patch a
        // RelicModel and would otherwise always be reported.
        var gameRelicIds = typeof(RelicModel).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(RelicModel).IsAssignableFrom(type))
            .Select(type => RelicIdHelper.Slugify(type.Name))
            .ToHashSet();

        var untracked = RelicStatsRegistry.All.Keys
            .Where(relicId => gameRelicIds.Contains(relicId) && !patchedRelics.Contains(relicId))
            .OrderBy(relicId => relicId)
            .ToList();

        if (untracked.Count == 0) return;

        Logger.Warn(
            $"{untracked.Count} relic(s) have no live patch on this game version and will record " +
            $"nothing: {string.Join(", ", untracked)}");
    }
}
