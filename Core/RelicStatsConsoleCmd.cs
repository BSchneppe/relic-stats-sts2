using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
#if DEBUG
using RelicStats.Core.Testing;
#endif

namespace RelicStats.Core;

public class RelicStatsConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "relicstats";
#if DEBUG
    public override string Args => "[dump|reset|test [relic_id|results]]";
    public override string Description => "Dump, reset, or test all relic stat descriptions.";
#else
    public override string Args => "[dump|reset]";
    public override string Description => "Dump or reset all relic stat descriptions.";
#endif
    public override bool IsNetworked => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (args.Length > 0 && args[0].Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            RelicStatsRegistry.ResetAll();
            return new CmdResult(true, $"Reset all {RelicStatsRegistry.All.Count} relic stats.");
        }

#if DEBUG
        if (args.Length > 0 && args[0].Equals("test", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessTest(issuingPlayer, args);
        }
#endif

        DumpAllDescriptions(issuingPlayer);

        var lines = new StringBuilder();
        lines.AppendLine("---");

        foreach (var (id, stats) in RelicStatsRegistry.All)
        {
            int effectiveTurns = 1, effectiveCombats = 1;
            if (issuingPlayer != null)
            {
                var floorMelted = RelicStatsRegistry.GetFloorMelted(id);
                var relic = issuingPlayer.Relics.FirstOrDefault(r => r.Id.Entry == id);
                if (relic != null)
                    (effectiveTurns, effectiveCombats) = MapHistoryHelper.GetEffective(
                        issuingPlayer, relic.FloorAddedToDeck, floorMelted);
            }
            var desc = stats.GetDescription(effectiveTurns, effectiveCombats);
            var plain = Regex.Replace(desc, @"\[/?[^\]]+\]", "");
            lines.AppendLine($"{id}: {plain}");
        }

        return new CmdResult(true, lines.ToString());
    }

    private static void DumpAllDescriptions(Player? player)
    {
        MainFile.Logger.Info($"=== Relic Stats Dump ===");
        foreach (var (id, stats) in RelicStatsRegistry.All)
        {
            int effectiveTurns = 1, effectiveCombats = 1;
            if (player != null)
            {
                var floorMelted = RelicStatsRegistry.GetFloorMelted(id);
                var relic = player.Relics.FirstOrDefault(r => r.Id.Entry == id);
                if (relic != null)
                    (effectiveTurns, effectiveCombats) = MapHistoryHelper.GetEffective(
                        player, relic.FloorAddedToDeck, floorMelted);
            }
            var desc = stats.GetDescription(effectiveTurns, effectiveCombats);
            var plain = Regex.Replace(desc, @"\[/?[^\]]+\]", "");
            MainFile.Logger.Info($"  [{id}] {plain}");
        }
        MainFile.Logger.Info($"=== End Dump ({RelicStatsRegistry.All.Count} relics) ===");
    }

#if DEBUG
    private static CmdResult ProcessTest(Player? issuingPlayer, string[] args)
    {
        TestHelpers.Player = issuingPlayer;

        if (args.Length > 1 && args[1].Equals("results", StringComparison.OrdinalIgnoreCase))
        {
            TestManager.ForceTimeoutCheck();
            return new CmdResult(true, TestManager.FormatResults());
        }

        if (TestManager.IsRunning)
        {
            return new CmdResult(false, "A test is already running. Use 'relicstats test results' to check progress.");
        }

        if (args.Length > 1)
        {
            if (args[1].Equals("failed", StringComparison.OrdinalIgnoreCase))
            {
                TestManager.RunFailed();
                return new CmdResult(true, "Rerunning failed tests...");
            }

            var relicId = args[1].ToUpperInvariant();
            if (RelicStatsRegistry.Get(relicId) == null)
                return new CmdResult(false, $"Unknown relic: {relicId}");

            TestManager.RunSingle(relicId);
            return new CmdResult(true, $"Test running for {relicId}...");
        }

        TestManager.RunAll();
        return new CmdResult(true, $"Running tests for {RelicStatsRegistry.All.Count} relics...");
    }
#endif
}
