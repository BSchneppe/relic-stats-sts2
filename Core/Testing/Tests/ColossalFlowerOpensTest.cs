#if DEBUG
using System.Linq;
using MegaCrit.Sts2.Core.Models;

namespace RelicStats.Core.Testing;

/// <summary>
/// Verifies Colossal Flower event loads without soft lock.
/// Crash path: ReachDeeper → HoverTipFactory.FromRelic&lt;PollinousCore&gt;()
/// → HoverTips getter → HoverTipsPatch → .Owner → CanonicalModelException
/// </summary>
public sealed class ColossalFlowerOpensTest : ValidationTest
{
    public override string RelicId => "COLOSSAL_FLOWER_OPENS";

    public override void RegisterTest(TestRunner runner)
    {
        runner.Assert("hovertips safe on canonical POLLINOUS_CORE", () =>
        {
            var canonical = ModelDb.AllRelics.First(r => r.Id.Entry == "POLLINOUS_CORE");
            _ = canonical.HoverTips;
            return new TestResult(true);
        });

        runner.Do("open colossal flower", () => TestHelpers.OpenEvent("COLOSSAL_FLOWER"));
        runner.WaitFor(GameEvent.RoomEntered, 10000);
        runner.Assert("colossal flower loaded", () =>
            new TestResult(true, "event loaded successfully"));
    }
}
#endif
