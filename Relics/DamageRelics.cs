using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;
using RelicStats.Core;
using RelicStats.Patches;
#if DEBUG
using RelicStats.Core.Testing;
#endif

namespace RelicStats.Relics;

// --- Direct damage relics ---

[HarmonyPatch(typeof(CharonsAshes), nameof(CharonsAshes.AfterCardExhausted))]
public sealed class CharonsAshesStats : MeasuredCounterStats<CharonsAshes>
{
    protected override string PreviousMeasurement => "base damage contribution";
    public override string Format => "Dealt {0} [gold]Damage[/gold] to Block and HP.";
    public static void Prefix(CharonsAshes __instance, out IDisposable __state) =>
        __state = DirectDamageScope.Begin(__instance.Owner,
            results => Track(__instance, s => s.Amount += DirectDamageScope.DamageDealt(results)));

    public static void Finalizer(IDisposable __state) => __state.Dispose();

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        int beforeLethal = 0;
        int lethalTargets = 0;
        Creature[] lethalEnemies = Array.Empty<Creature>();
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("exhaust a card", () => {
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.ExhaustCard();
        });
        runner.WaitFor(GameEvent.CardExhausted);
        runner.Assert("tracked damage", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var enemyCount = TestHelpers.Player!.Creature.CombatState!.HittableEnemies.Count;
            var expected = relic!.DynamicVars.Damage.IntValue * enemyCount;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Do("finish all enemies with one relic hit", () => {
            beforeLethal = Amount;
            var enemies = TestHelpers.Player!.Creature.CombatState!.HittableEnemies.ToArray();
            lethalEnemies = enemies;
            lethalTargets = enemies.Length;
            foreach (var enemy in enemies)
            {
                enemy.LoseBlockInternal(enemy.Block);
                enemy.SetCurrentHpInternal(1);
            }
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.ExhaustCard();
        });
        runner.WaitUntil("the final relic damage completed", () =>
            lethalTargets > 0 && Amount - beforeLethal == lethalTargets, 15000);
        runner.Assert("counted final lethal HP without overkill", () =>
            new TestResult(lethalTargets > 0 && Amount - beforeLethal == lethalTargets &&
                lethalEnemies.All(enemy => enemy.IsDead),
                $"expected {lethalTargets} actual HP damage and no surviving target, got {Amount - beforeLethal}; " +
                $"{lethalEnemies.Count(enemy => enemy.IsAlive)} targets alive"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

[HarmonyPatch(typeof(FestivePopper), nameof(FestivePopper.AfterPlayerTurnStart))]
public sealed class FestivePopperStats : MeasuredCounterStats<FestivePopper>
{
    protected override string PreviousMeasurement => "base damage contribution";
    public override string Format => "Dealt {0} [gold]Damage[/gold] to Block and HP.";
    public override StatCadence Cadence => StatCadence.Combat;
    public static void Prefix(FestivePopper __instance, out IDisposable __state) =>
        __state = DirectDamageScope.Begin(__instance.Owner,
            results => Track(__instance, s => s.Amount += DirectDamageScope.DamageDealt(results)));

    public static void Finalizer(IDisposable __state) => __state.Dispose();

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked damage", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var enemyCount = TestHelpers.Player!.Creature.CombatState!.HittableEnemies.Count;
            var expected = relic!.DynamicVars.Damage.IntValue * enemyCount;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        // Turn 1 only: the start of turn 2 must not count.
        runner.Do("end turn 1", () => { DamageMeasurementTestSafety.ProtectEnemies(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("no damage on turn 2", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var enemyCount = TestHelpers.Player!.Creature.CombatState!.HittableEnemies.Count;
            var expected = relic!.DynamicVars.Damage.IntValue * enemyCount;
            return new TestResult(Amount == expected, $"expected still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

[HarmonyPatch(typeof(Kusarigama), nameof(Kusarigama.AfterCardPlayed))]
public sealed class KusarigamaStats : MeasuredCounterStats<Kusarigama>
{
    protected override string PreviousMeasurement => "base damage contribution";
    public override string Format => "Dealt {0} [gold]Damage[/gold] to Block and HP.";
    public static void Prefix(Kusarigama __instance, out IDisposable __state) =>
        __state = DirectDamageScope.Begin(__instance.Owner,
            results => Track(__instance, s => s.Amount += DirectDamageScope.DamageDealt(results)));

    public static void Finalizer(IDisposable __state) => __state.Dispose();

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("add energy + protect enemy + play 3 attacks", () => {
            TestHelpers.AddEnergy(10);
            DamageMeasurementTestSafety.ProtectEnemies();
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.PlayThenEndTurn(3, 0);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked damage", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Damage.IntValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        // Every 3 attacks per turn: two attacks on turn 2 stay one short of the threshold
        // (every card in hand is a Strike, whether redrawn or freshly spawned).
        runner.Do("play 2 strikes on turn 2", () => {
            TestHelpers.AddEnergy(10);
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.PlayThenEndTurn(2, 0);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("one short of the threshold does not count", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Damage.IntValue;
            return new TestResult(Amount == expected, $"expected still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

[HarmonyPatch(typeof(LetterOpener), nameof(LetterOpener.AfterCardPlayed))]
public sealed class LetterOpenerStats : MeasuredCounterStats<LetterOpener>
{
    protected override string PreviousMeasurement => "base damage contribution";
    public override string Format => "Dealt {0} [gold]Damage[/gold] to Block and HP.";
    public static void Prefix(LetterOpener __instance, out IDisposable __state) =>
        __state = DirectDamageScope.Begin(__instance.Owner,
            results => Track(__instance, s => s.Amount += DirectDamageScope.DamageDealt(results)));

    public static void Finalizer(IDisposable __state) => __state.Dispose();

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("add energy + protect enemy + play 3 skills", () => {
            TestHelpers.AddEnergy(10);
            DamageMeasurementTestSafety.ProtectEnemies();
            TestHelpers.SpawnCard("DEFEND");
            TestHelpers.SpawnCard("DEFEND");
            TestHelpers.SpawnCard("DEFEND");
            TestHelpers.PlayThenEndTurn(3);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked damage", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var enemyCount = TestHelpers.Player!.Creature.CombatState!.HittableEnemies.Count;
            var expected = relic!.DynamicVars.Damage.IntValue * enemyCount;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        // Every 3 skills per turn: two skills on turn 2 stay one short of the threshold
        // (every card in hand is a Defend, whether redrawn or freshly spawned).
        runner.Do("play 2 defends on turn 2", () => {
            TestHelpers.AddEnergy(10);
            TestHelpers.SpawnCard("DEFEND");
            TestHelpers.SpawnCard("DEFEND");
            TestHelpers.PlayThenEndTurn(2);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("one short of the threshold does not count", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var enemyCount = TestHelpers.Player!.Creature.CombatState!.HittableEnemies.Count;
            var expected = relic!.DynamicVars.Damage.IntValue * enemyCount;
            return new TestResult(Amount == expected, $"expected still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

[HarmonyPatch(typeof(MercuryHourglass), nameof(MercuryHourglass.AfterPlayerTurnStart))]
public sealed class MercuryHourglassStats : MeasuredCounterStats<MercuryHourglass>
{
    protected override string PreviousMeasurement => "base damage contribution";
    public override string Format => "Dealt {0} [gold]Damage[/gold] to Block and HP.";
    public static void Prefix(MercuryHourglass __instance, out IDisposable __state) =>
        __state = DirectDamageScope.Begin(__instance.Owner,
            results => Track(__instance, s => s.Amount += DirectDamageScope.DamageDealt(results)));

    public static void Finalizer(IDisposable __state) => __state.Dispose();

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked damage", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var enemyCount = TestHelpers.Player!.Creature.CombatState!.HittableEnemies.Count;
            var expected = relic!.DynamicVars.Damage.IntValue * enemyCount;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

[HarmonyPatch(typeof(MrStruggles), nameof(MrStruggles.AfterPlayerTurnStart))]
public sealed class MrStrugglesStats : MeasuredCounterStats<MrStruggles>
{
    protected override string PreviousMeasurement => "base damage contribution";
    public override string Format => "Dealt {0} [gold]Damage[/gold] to Block and HP.";
    public static void Prefix(MrStruggles __instance, out IDisposable __state) =>
        __state = DirectDamageScope.Begin(__instance.Owner,
            results => Track(__instance, s => s.Amount += DirectDamageScope.DamageDealt(results)));

    public static void Finalizer(IDisposable __state) => __state.Dispose();

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        // The relic deals TurnNumber damage to every hittable enemy each player turn; the tracker adds
        // TurnNumber * HittableEnemies.Count. One Nibbit: 1 on turn 1, then 1 + 2 = 3 by turn 2.
        runner.Assert("tracked turn-1 damage", () =>
            new TestResult(Amount == 1, $"expected 1 (turn 1 x 1 enemy), got {Amount}"));
        runner.Do("god mode + protect enemy + end turn", () => { DamageMeasurementTestSafety.ProtectPlayer(); DamageMeasurementTestSafety.ProtectEnemies(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked turn-2 damage on top", () =>
            new TestResult(Amount == 3, $"expected 3 (1 + 2 against one Nibbit), got {Amount}"));
        runner.Cleanup(() => { DamageMeasurementTestSafety.ProtectPlayer(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

[HarmonyPatch(typeof(ScreamingFlagon), nameof(ScreamingFlagon.BeforeSideTurnEnd))]
public sealed class ScreamingFlagonStats : MeasuredCounterStats<ScreamingFlagon>
{
    protected override string PreviousMeasurement => "base damage contribution";
    public override string Format => "Dealt {0} [gold]Damage[/gold] to Block and HP.";
    public static void Prefix(ScreamingFlagon __instance, out IDisposable __state) =>
        __state = DirectDamageScope.Begin(__instance.Owner,
            results => Track(__instance, s => s.Amount += DirectDamageScope.DamageDealt(results)));

    public static void Finalizer(IDisposable __state) => __state.Dispose();

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        int expected = -1;
        runner.Do("discard hand then end turn", () => {
            DamageMeasurementTestSafety.ProtectEnemies();
            TestHelpers.DiscardHand();
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var enemyCount = TestHelpers.Player!.Creature.CombatState!.HittableEnemies.Count;
            expected = relic!.DynamicVars.Damage.IntValue * enemyCount;
            TestHelpers.EndTurn();
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked damage", () =>
            new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}"));
        // Empty hand only: ending turn 2 with a card still in hand must not count.
        runner.Do("end turn 2 holding a card", () => {
            TestHelpers.SpawnCard("STRIKE_IRONCLAD");
            TestHelpers.EndTurn();
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("no damage with a card in hand", () =>
            new TestResult(Amount == expected, $"expected still {expected}, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

[HarmonyPatch(typeof(StoneCalendar), nameof(StoneCalendar.BeforeSideTurnEnd))]
public sealed class StoneCalendarStats : MeasuredCounterStats<StoneCalendar>
{
    protected override string PreviousMeasurement => "base damage contribution";
    public override string Format => "Dealt {0} [gold]Damage[/gold] to Block and HP.";
    public override StatCadence Cadence => StatCadence.Combat;
    public static void Prefix(StoneCalendar __instance, out IDisposable __state) =>
        __state = DirectDamageScope.Begin(__instance.Owner,
            results => Track(__instance, s => s.Amount += DirectDamageScope.DamageDealt(results)));

    public static void Finalizer(IDisposable __state) => __state.Dispose();

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("enable god mode + protect enemy", () => { DamageMeasurementTestSafety.ProtectPlayer(); DamageMeasurementTestSafety.ProtectEnemies(); });
        // End turns 1-6 to reach round 7 where StoneCalendar triggers.
        // Use longer per-step timeout (8s) to prevent timeout on slower turns.
        for (int i = 1; i <= 6; i++)
        {
            runner.Do($"end turn {i}", () => TestHelpers.EndTurn());
            runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        }
        // Turn DamageTurn only: nothing may have counted before turn 7 ends.
        runner.Assert("nothing before the damage turn", () =>
            new TestResult(Amount == 0, $"expected 0 after six end turns, got {Amount}"));
        // Now end turn 7 — the relic fires in BeforeSideTurnEnd when TurnNumber == DamageTurn
        int expected = -1;
        runner.Do("end turn 7", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var enemyCount = TestHelpers.Player!.Creature.CombatState!.HittableEnemies.Count;
            expected = relic!.DynamicVars.Damage.IntValue * enemyCount;
            TestHelpers.EndTurn();
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked damage", () =>
            new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}"));
        runner.Cleanup(() => { DamageMeasurementTestSafety.ProtectPlayer(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

[HarmonyPatch(typeof(Tingsha), nameof(Tingsha.AfterCardDiscarded))]
public sealed class TingshaStats : MeasuredCounterStats<Tingsha>
{
    protected override string PreviousMeasurement => "base damage contribution";
    public override string Format => "Dealt {0} [gold]Damage[/gold] to Block and HP.";
    public static void Prefix(Tingsha __instance, out IDisposable __state) =>
        __state = DirectDamageScope.Begin(__instance.Owner,
            results => Track(__instance, s => s.Amount += DirectDamageScope.DamageDealt(results)));

    public static void Finalizer(IDisposable __state) => __state.Dispose();

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("discard a card", () => {
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.DiscardCard();
        });
        runner.WaitFor(GameEvent.CardDiscarded);
        runner.Assert("tracked damage", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Damage.IntValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// --- ModifyDamageAdditive relics ---
// Measure non-preview additive damage-value contributions, including values used by other
// effects such as Thrash. These are not claimed as realized hit damage.

[HarmonyPatch(typeof(FakeStrikeDummy), nameof(FakeStrikeDummy.ModifyDamageAdditive))]
public sealed class FakeStrikeDummyStats : SimpleCounterStats<FakeStrikeDummy>
{
    public override string Format => "Contributed {0} base [gold]Damage[/gold] to Strike values and effects.";
    public static void Postfix(decimal __result, FakeStrikeDummy __instance, CardModel? cardSource)
    {
        if (__result == 0m || cardSource == null || DamagePreviewScope.IsPreview) return;
        Track(__instance, s => s.Amount += (int)__result);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        // Strikes only: an attack without the Strike tag (Bash) must not count.
        runner.Do("play non-strike attack", () => {
            TestHelpers.AddEnergy(10);
            DamageMeasurementTestSafety.ProtectEnemies();
            TestHelpers.SpawnCard("BASH");
            TestHelpers.PlayCard(0, 0);
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("non-strike attack does not count", () =>
            new TestResult(Amount == 0, $"expected 0 after Bash, got {Amount}"));
        runner.Do("play strike", () => {
            TestHelpers.SpawnCard("STRIKE_IRONCLAD");
            TestHelpers.PlayThenEndTurn(1, 0);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked damage", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = (int)relic!.DynamicVars["ExtraDamage"].BaseValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

[HarmonyPatch(typeof(StrikeDummy), nameof(StrikeDummy.ModifyDamageAdditive))]
public sealed class StrikeDummyStats : SimpleCounterStats<StrikeDummy>
{
    public override string Format => "Contributed {0} base [gold]Damage[/gold] to Strike values and effects.";
    public static void Postfix(decimal __result, StrikeDummy __instance, CardModel? cardSource)
    {
        if (__result == 0m || cardSource == null || DamagePreviewScope.IsPreview) return;
        Track(__instance, s => s.Amount += (int)__result);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        // Strikes only: an attack without the Strike tag (Bash) must not count.
        runner.Do("play non-strike attack", () => {
            TestHelpers.AddEnergy(10);
            DamageMeasurementTestSafety.ProtectEnemies();
            TestHelpers.SpawnCard("BASH");
            TestHelpers.PlayCard(0, 0);
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("non-strike attack does not count", () =>
            new TestResult(Amount == 0, $"expected 0 after Bash, got {Amount}"));
        runner.Do("play strike", () => {
            TestHelpers.SpawnCard("STRIKE_IRONCLAD");
            TestHelpers.PlayThenEndTurn(1, 0);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked damage", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = (int)relic!.DynamicVars["ExtraDamage"].BaseValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

[HarmonyPatch(typeof(MiniatureCannon), nameof(MiniatureCannon.ModifyDamageAdditive))]
public sealed class MiniatureCannonStats : SimpleCounterStats<MiniatureCannon>
{
    public override string Format => "Contributed {0} base [gold]Damage[/gold] to upgraded attack values and effects.";
    public static void Postfix(decimal __result, MiniatureCannon __instance, CardModel? cardSource)
    {
        if (__result == 0m || cardSource == null || DamagePreviewScope.IsPreview) return;
        Track(__instance, s => s.Amount += (int)__result);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // ModifyDamageAdditive fires for upgraded attacks only: an unupgraded Strike must not
        // count, then an upgraded one does.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("play unupgraded strike", () => {
            TestHelpers.AddEnergy(10);
            DamageMeasurementTestSafety.ProtectEnemies();
            TestHelpers.SpawnCard("STRIKE_IRONCLAD");
            TestHelpers.PlayCard(0, 0);
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("unupgraded card does not count", () =>
            new TestResult(Amount == 0, $"expected 0 after an unupgraded Strike, got {Amount}"));
        runner.Do("spawn, upgrade, and play strike", () => {
            TestHelpers.SpawnCard("STRIKE_IRONCLAD");
            TestHelpers.UpgradeCard();
            TestHelpers.PlayThenEndTurn(1, 0);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked bonus for upgraded card", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = (int)(relic?.DynamicVars["ExtraDamage"]?.BaseValue ?? 0);
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

[HarmonyPatch(typeof(MysticLighter), nameof(MysticLighter.ModifyDamageAdditive))]
public sealed class MysticLighterStats : SimpleCounterStats<MysticLighter>
{
    public override string Format => "Contributed {0} base [gold]Damage[/gold] to enchanted attack values and effects.";
    public static void Postfix(decimal __result, MysticLighter __instance, CardModel? cardSource)
    {
        if (__result == 0m || cardSource == null || DamagePreviewScope.IsPreview) return;
        Track(__instance, s => s.Amount += (int)__result);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // ModifyDamageAdditive fires for enchanted attacks only: a plain Strike must not count,
        // then a Sharp-enchanted one does.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("play unenchanted strike", () => {
            TestHelpers.AddEnergy(10);
            DamageMeasurementTestSafety.ProtectEnemies();
            TestHelpers.SpawnCard("STRIKE_IRONCLAD");
            TestHelpers.PlayCard(0, 0);
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("unenchanted card does not count", () =>
            new TestResult(Amount == 0, $"expected 0 after a plain Strike, got {Amount}"));
        runner.Do("spawn, enchant, play strike, then end turn", () => {
            TestHelpers.SpawnCard("STRIKE_IRONCLAD");
            TestHelpers.EnchantCard("SHARP");
            TestHelpers.PlayThenEndTurn(1, 0);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked bonus for enchanted card", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Damage.IntValue ?? 0;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// --- Additional damage relics ---

// ForgottenSoul: deals damage to a random enemy on exhaust
[HarmonyPatch(typeof(ForgottenSoul), nameof(ForgottenSoul.AfterCardExhausted))]
public sealed class ForgottenSoulStats : MeasuredCounterStats<ForgottenSoul>
{
    protected override string PreviousMeasurement => "base damage contribution";
    public override string Format => "Dealt {0} [gold]Damage[/gold] to Block and HP.";
    public static void Prefix(ForgottenSoul __instance, out IDisposable __state) =>
        __state = DirectDamageScope.Begin(__instance.Owner,
            results => Track(__instance, s => s.Amount += DirectDamageScope.DamageDealt(results)));

    public static void Finalizer(IDisposable __state) => __state.Dispose();

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("exhaust a card", () => {
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.ExhaustCard();
        });
        runner.WaitFor(GameEvent.CardExhausted);
        runner.Assert("tracked damage", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Damage.IntValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// LostWisp: deals damage to all enemies when a Power is played
[HarmonyPatch(typeof(LostWisp), nameof(LostWisp.AfterCardPlayed))]
public sealed class LostWispStats : MeasuredCounterStats<LostWisp>
{
    protected override string PreviousMeasurement => "base damage contribution";
    public override string Format => "Dealt {0} [gold]Damage[/gold] to Block and HP.";
    public static void Prefix(LostWisp __instance, out IDisposable __state) =>
        __state = DirectDamageScope.Begin(__instance.Owner,
            results => Track(__instance, s => s.Amount += DirectDamageScope.DamageDealt(results)));

    public static void Finalizer(IDisposable __state) => __state.Dispose();

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        // Powers only: an Attack must not count.
        runner.Do("play attack", () => {
            TestHelpers.AddEnergy(10);
            DamageMeasurementTestSafety.ProtectEnemies();
            TestHelpers.SpawnCard("STRIKE_IRONCLAD");
            TestHelpers.PlayCard(0, 0);
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("attack does not count", () =>
            new TestResult(Amount == 0, $"expected 0 after an attack, got {Amount}"));
        runner.Do("play power", () => {
            TestHelpers.SpawnCard("DEMON_FORM");
            TestHelpers.PlayThenEndTurn();
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked damage", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var enemyCount = TestHelpers.Player!.Creature.CombatState!.HittableEnemies.Count;
            var expected = relic!.DynamicVars.Damage.IntValue * enemyCount;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// ParryingShield: deals damage to a random enemy at turn end if block >= threshold
[HarmonyPatch(typeof(ParryingShield), nameof(ParryingShield.AfterSideTurnEnd))]
public sealed class ParryingShieldStats : MeasuredCounterStats<ParryingShield>
{
    protected override string PreviousMeasurement => "base damage contribution";
    public override string Format => "Dealt {0} [gold]Damage[/gold] to Block and HP.";
    public static void Prefix(ParryingShield __instance, out IDisposable __state) =>
        __state = DirectDamageScope.Begin(__instance.Owner,
            results => Track(__instance, s => s.Amount += DirectDamageScope.DamageDealt(results)));

    public static void Finalizer(IDisposable __state) => __state.Dispose();

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("give block and end turn", () => { TestHelpers.GiveBlock(99); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked damage", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Damage.IntValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        // Block threshold: ending turn 2 with no block must not count.
        runner.Do("end turn 2 without block", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("no damage below the block threshold", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Damage.IntValue;
            return new TestResult(Amount == expected, $"expected still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// TheBoot: boosts low damage hits to 5
[HarmonyPatch(typeof(TheBoot), nameof(TheBoot.ModifyHpLostAfterOstyLate))]
public sealed class TheBootStats : MeasuredCounterStats<TheBoot>
{
    protected override string PreviousMeasurement => "low-hit boosts";
    public override string Format => "Added {0} [gold]Damage[/gold] to low hits.";
    public static void Postfix(decimal __result, TheBoot __instance, decimal amount)
    {
        int added = (int)(__result - amount);
        if (added <= 0) return;
        Track(__instance, s => s.Amount += added);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // TheBoot boosts attack hits below DamageMinimum (5) to 5. A Shiv deals 4 and is boosted;
        // a Strike deals 6 and is not. No god mode: its Strength would push every hit past 5.
        // ModifyHpLost runs only in CreatureCmd.Damage (not previews), once per hit, and the
        // returned modifier delta also covers the owner Osty, while self-targets return no delta.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        // Both on turn 1, while the Nibbit has no block: the Strike lands 6 unblocked and must not count.
        runner.Do("play strike (above the minimum)", () => {
            TestHelpers.AddEnergy(10);
            DamageMeasurementTestSafety.ProtectEnemies();
            TestHelpers.SpawnCard("STRIKE_IRONCLAD");
            TestHelpers.PlayCard(0, 0);
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("hit at or above the minimum does not count", () =>
            new TestResult(Amount == 0, $"expected 0 after a 6-damage Strike, got {Amount}"));
        runner.Do("play shiv (low damage)", () => {
            TestHelpers.SpawnCard("SHIV");
            TestHelpers.PlayThenEndTurn(1, 0);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked boost for low-damage card", () =>
            new TestResult(Amount == 1, $"expected 1 boost for a 4-damage Shiv, got {Amount}"));
        runner.Do("low owner Osty hit contributes four", () => {
            TestHelpers.SpawnCard("BODYGUARD");
            TestHelpers.AddEnergy(10);
            TestHelpers.PlayCard(PileType.Hand.GetPile(TestHelpers.Player!).Cards.Count - 1);
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Do("measure owner Osty boost", () => {
            var relic = TestHelpers.GetRelic<TheBoot>()!;
            var owner = TestHelpers.Player!;
            var enemy = owner.Creature.CombatState!.HittableEnemies[0];
            relic.ModifyHpLostAfterOstyLate(enemy, 1m, ValueProp.Move, owner.Osty, null);
        });
        runner.Assert("includes owner Osty modifier contribution", () =>
            new TestResult(Amount == 5, $"expected 1 Shiv + 4 Osty damage added, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// ThrowingAxe: doubles first card play each combat
// ModifyCardPlayCount is a pure calculation the game also runs outside the real play (the test log
// shows it once before "playing card" and once during), so counting its result double-counts. The
// relic marks itself used in AfterModifyingCardPlayCount, which only runs for the real play; count
// that false -> true flip instead: at most once per combat, which is what the relic does.
[HarmonyPatch(typeof(ThrowingAxe), nameof(ThrowingAxe.AfterModifyingCardPlayCount))]
public sealed class ThrowingAxeStats : SimpleCounterStats<ThrowingAxe>
{
    private static readonly FieldInfo UsedField =
        AccessTools.Field(typeof(ThrowingAxe), "_usedThisCombat");

    public override string Format => "Doubled first card {0} times.";
    public override StatCadence Cadence => StatCadence.Total;

    public static void Prefix(ThrowingAxe __instance, out bool __state) =>
        __state = (bool)UsedField.GetValue(__instance)!;

    public static void Postfix(ThrowingAxe __instance, bool __state, CardModel card)
    {
        if (__state) return; // already used earlier this combat
        if (card.Owner != __instance.Owner) return;
        if (!(bool)UsedField.GetValue(__instance)!) return; // this call didn't consume the relic
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("play card then end turn", () => {
            TestHelpers.AddEnergy(10);
            DamageMeasurementTestSafety.ProtectEnemies();
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.PlayThenEndTurn(1, 0);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked stat", () =>
            new TestResult(Amount == 1, $"expected 1, got {Amount}"));
        // First card per combat only: a Strike on turn 2 must not be doubled again.
        runner.Do("play a strike on turn 2", () => {
            TestHelpers.AddEnergy(10);
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.PlayThenEndTurn(1, 0);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("second card is not doubled", () =>
            new TestResult(Amount == 1, $"expected still 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}
