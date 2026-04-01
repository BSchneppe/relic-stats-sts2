#if DEBUG
using System.Linq;

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

        runner.Cleanup(() =>
        {
            TestHelpers.RemoveRelic("ANCHOR");
            RelicStatsRegistry.Get("ANCHOR")?.Reset();
        });
    }
}
#endif
