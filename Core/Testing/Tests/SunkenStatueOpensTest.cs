#if DEBUG
using System.Linq;
using MegaCrit.Sts2.Core.Models;

namespace RelicStats.Core.Testing;

/// <summary>
/// Verifies Sunken Statue event loads
/// Crash path: GenerateInitialOptions → HoverTipFactory.FromRelic&lt;SwordOfStone&gt;()
/// → HoverTips getter → HoverTipsPatch → .Owner → CanonicalModelException
/// </summary>
public sealed class SunkenStatueOpensTest : ValidationTest
{
    public override string RelicId => "SUNKEN_STATUE_OPENS";

    public override void RegisterTest(TestRunner runner)
    {
        // The crash occurs when HoverTipFactory.FromRelic<SwordOfStone>() accesses
        // canonical model HoverTips during GenerateInitialOptions.
        runner.Assert("hovertips safe on canonical SWORD_OF_STONE", () =>
        {
            var canonical = ModelDb.AllRelics.First(r => r.Id.Entry == "SWORD_OF_STONE");
            _ = canonical.HoverTips;
            return new TestResult(true);
        });

        runner.Do("open sunken statue", () => TestHelpers.OpenEvent("SUNKEN_STATUE"));
        runner.WaitFor(GameEvent.RoomEntered, 10000);
        runner.Assert("sunken statue loaded", () =>
            new TestResult(true, "event loaded successfully"));
    }
}
#endif
