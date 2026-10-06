#if DEBUG
using System.Linq;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Models.Relics;

namespace RelicStats.Core.Testing;

/// <summary>
/// Verifies that per-turn/per-combat averages survive save/load.
/// With the derived approach, averages come from MapPointHistory (game-persisted)
/// and Amount (our persistence). This test verifies our Amount round-trips correctly.
/// </summary>
public sealed class SaveLoadAveragesTest : ValidationTest
{
    public override string RelicId => "SAVE_LOAD_AVERAGES";

    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic and set amount", () =>
        {
            TestHelpers.AddRelic("ANCHOR");
            var stats = RelicStatsRegistry.Get("ANCHOR");
            if (stats is SimpleCounterStats<MegaCrit.Sts2.Core.Models.Relics.Anchor> anchor)
                anchor.Amount = 42;
        });

        runner.Assert("pre-save: Amount is 42", () =>
        {
            var stats = RelicStatsRegistry.Get("ANCHOR");
            if (stats is SimpleCounterStats<MegaCrit.Sts2.Core.Models.Relics.Anchor> anchor)
                return new TestResult(anchor.Amount == 42, $"expected 42, got {anchor.Amount}");
            return new TestResult(false, "stats not found");
        });

        runner.Do("save, reset, load", () =>
        {
            StatsPersistence.Save(isMultiplayer: false);
            RelicStatsRegistry.ResetAll();
            StatsPersistence.Load(isMultiplayer: false);
        });

        runner.Assert("post-load: Amount preserved", () =>
        {
            var stats = RelicStatsRegistry.Get("ANCHOR");
            if (stats is SimpleCounterStats<MegaCrit.Sts2.Core.Models.Relics.Anchor> anchor)
                return new TestResult(anchor.Amount == 42, $"expected 42, got {anchor.Amount}");
            return new TestResult(false, "stats not found");
        });

        runner.Do("migrate legacy nominal damage and persist completed damage", () =>
        {
            var stats = (SimpleCounterStats<CharonsAshes>)RelicStatsRegistry.Get("CHARONS_ASHES")!;
            stats.Load(new JsonObject { ["amount"] = 99 });
        });

        runner.Assert("old damage remains separate from the completed measurement", () =>
        {
            var stats = (SimpleCounterStats<CharonsAshes>)RelicStatsRegistry.Get("CHARONS_ASHES")!;
            var saved = stats.Save();
            return new TestResult(stats.Amount == 0 && saved["previousAmount"]?.GetValue<int>() == 99
                && stats.GetDescription(10, 5).Contains("Measured since update."),
                $"completed={stats.Amount}, saved={saved}");
        });

        runner.Do("round trip both damage generations", () =>
        {
            var stats = (SimpleCounterStats<CharonsAshes>)RelicStatsRegistry.Get("CHARONS_ASHES")!;
            stats.Amount = 17;
            var saved = stats.Save();
            stats.Reset();
            stats.Load(saved);
        });

        runner.Assert("completed and earlier nominal damage both survive", () =>
        {
            var stats = (SimpleCounterStats<CharonsAshes>)RelicStatsRegistry.Get("CHARONS_ASHES")!;
            var saved = stats.Save();
            return new TestResult(stats.Amount == 17 && saved["previousAmount"]?.GetValue<int>() == 99
                && saved["measurementVersion"]?.GetValue<int>() == 1,
                $"completed={stats.Amount}, saved={saved}");
        });

        runner.Cleanup(() =>
        {
            TestHelpers.RemoveRelic("ANCHOR");
            RelicStatsRegistry.Get("ANCHOR")?.Reset();
            RelicStatsRegistry.Get("CHARONS_ASHES")?.Reset();
        });
    }
}
#endif
