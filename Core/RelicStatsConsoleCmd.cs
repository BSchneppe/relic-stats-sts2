using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;

namespace RelicStats.Core;

public class RelicStatsConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "relicstats";
    public override string Args => "[dump|reset]";
    public override string Description => "Dump or reset all relic stat descriptions.";
    public override bool IsNetworked => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (args.Length > 0 && args[0].Equals("reset", System.StringComparison.OrdinalIgnoreCase))
        {
            RelicStatsRegistry.ResetAll();
            return new CmdResult(true, $"Reset all {RelicStatsRegistry.All.Count} relic stats.");
        }

        RelicStatsRegistry.DumpAllDescriptions();

        var lines = new System.Text.StringBuilder();
        lines.AppendLine($"Turn {RelicStatsRegistry.TurnCount}, Combat {RelicStatsRegistry.CombatCount}");
        lines.AppendLine($"---");

        foreach (var (id, stats) in RelicStatsRegistry.All)
        {
            var desc = stats.GetDescription(RelicStatsRegistry.TurnCount, RelicStatsRegistry.CombatCount);
            var plain = System.Text.RegularExpressions.Regex.Replace(desc, @"\[/?[^\]]+\]", "");
            lines.AppendLine($"{id}: {plain}");
        }

        return new CmdResult(true, lines.ToString());
    }
}
