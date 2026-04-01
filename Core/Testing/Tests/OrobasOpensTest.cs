#if DEBUG
using System.Linq;
using MegaCrit.Sts2.Core.Models;

namespace RelicStats.Core.Testing;

/// <summary>
/// Verifies the mod doesn't break Orobas Ancient dialogue.
/// Reproduces: Orobas, Colossal Flower, War Historian, Sunken Statue soft locks.
/// Root cause: HoverTipPatch accesses .Owner on canonical (immutable) models,
/// which throws CanonicalModelException during event setup.
/// </summary>
public sealed class OrobasOpensTest : ValidationTest
{
    public override string RelicId => "OROBAS_OPENS";

    public override void RegisterTest(TestRunner runner)
    {
        // TouchOfOrobas.SetupForPlayer → set_StarterRelic → HoverTips getter
        // → HoverTipsPatch.Postfix → .Owner → AssertMutable() → CanonicalModelException
        //
        // Access HoverTips on each starter relic's canonical model. If HoverTipPatch
        // doesn't handle canonical models, this throws and the test fails.
        runner.Assert("hovertips safe on canonical starter relics", () =>
        {
            string[] starters = { "BURNING_BLOOD", "RING_OF_THE_SNAKE", "DIVINE_RIGHT",
                                  "BOUND_PHYLACTERY", "CRACKED_CORE" };
            foreach (var id in starters)
            {
                var canonical = ModelDb.AllRelics.FirstOrDefault(r => r.Id.Entry == id);
                if (canonical == null) continue;
                // This triggers HoverTipsPatch — must not throw CanonicalModelException
                _ = canonical.HoverTips;
            }
            return new TestResult(true);
        });
    }
}
#endif
