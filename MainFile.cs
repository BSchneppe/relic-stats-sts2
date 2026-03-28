using System;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
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
    }
}
