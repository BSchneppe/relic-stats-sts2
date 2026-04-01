#if DEBUG
using System.Linq;
using MegaCrit.Sts2.Core.Models;

namespace RelicStats.Core.Testing;

/// <summary>
/// Verifies War Historian, Repy event loads without soft lock.
/// Crash path: GenerateInitialOptions → HoverTipFactory.FromRelic&lt;HistoryCourse&gt;()
/// → HoverTips getter → HoverTipsPatch → .Owner → CanonicalModelException
/// </summary>
public sealed class WarHistorianRepyOpensTest : ValidationTest
{
    public override string RelicId => "WAR_HISTORIAN_REPY_OPENS";

    public override void RegisterTest(TestRunner runner)
    {
        runner.Assert("hovertips safe on canonical HISTORY_COURSE", () =>
        {
            var canonical = ModelDb.AllRelics.First(r => r.Id.Entry == "HISTORY_COURSE");
            _ = canonical.HoverTips;
            return new TestResult(true);
        });

        runner.Do("open war historian repy", () => TestHelpers.OpenEvent("WAR_HISTORIAN_REPY"));
        runner.WaitFor(GameEvent.RoomEntered, 10000);
        runner.Assert("war historian repy loaded", () =>
            new TestResult(true, "event loaded successfully"));
    }
}
#endif
