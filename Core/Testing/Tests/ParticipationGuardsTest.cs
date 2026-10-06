#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;

namespace RelicStats.Core.Testing;

public sealed class ParticipationGuardsTest : ValidationTest
{
    public override string RelicId => "PARTICIPATION_GUARDS";

    public override void RegisterTest(TestRunner runner)
    {
        string[] ids = { "SAI", "LANTERN", "LUNAR_PASTRY", "ORANGE_DOUGH" };
        Dictionary<string, string>? before = null;
        Task? hooks = null;
        runner.Do("prepare representative owner relics", () =>
        {
            foreach (var id in ids) TestHelpers.AddRelic(id);
            TestHelpers.StartFight();
        });
        runner.WaitFor(GameEvent.SideTurnStart, 10000);
        runner.Do("dispatch real player-side hooks without owner participation", () =>
        {
            before = ids.ToDictionary(id => id, id => RelicStatsRegistry.Get(id)!.Save().ToJsonString());
            hooks = DispatchWithoutParticipants();
        });
        runner.WaitUntil("nonparticipant hooks complete", () =>
        {
            if (hooks?.IsFaulted == true) throw hooks.Exception!;
            return hooks?.IsCompletedSuccessfully == true;
        }, 10000);
        runner.Assert("nonparticipants do not add block, energy, draws, or activations", () =>
        {
            var changed = ids.Where(id => before![id] != RelicStatsRegistry.Get(id)!.Save().ToJsonString()).ToArray();
            return new TestResult(changed.Length == 0, "changed: " + string.Join(", ", changed));
        });
        runner.Cleanup(() =>
        {
            foreach (var id in ids) TestHelpers.RemoveRelic(id);
            TestHelpers.WinCombat();
        });
    }

    private static async Task DispatchWithoutParticipants()
    {
        var state = CombatManager.Instance.DebugOnlyGetState()!;
        var participants = Array.Empty<Creature>();
        await Hook.BeforeSideTurnStart(state, CombatSide.Player, participants);
        await Hook.AfterSideTurnStart(state, CombatSide.Player, participants);
        // These side-wide end hooks were added after stable 0.107. The earlier build still
        // verifies start hooks; current versions also exercise Lunar Pastry's real end hook.
        foreach (var name in new[] { "BeforeSideTurnEnd", "AfterSideTurnEnd" })
        {
            var method = AccessTools.DeclaredMethod(typeof(Hook), name);
            if (method?.Invoke(null, new object[] { state, CombatSide.Player, participants }) is Task task)
                await task;
        }
    }
}
#endif
