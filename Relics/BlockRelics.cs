using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using RelicStats.Core;
#if DEBUG
using RelicStats.Core.Testing;
#endif

namespace RelicStats.Relics;

// Anchor: gains block at combat start
[HarmonyPatch(typeof(Anchor), nameof(Anchor.BeforeCombatStart))]
public sealed class AnchorStats : SimpleCounterStats<Anchor>
{
    public override string Format => "Provided {0} base [gold]Block[/gold].";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(Anchor __instance) =>
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight("NIBBITS_WEAK"));
        runner.WaitFor(GameEvent.CombatStart);
        runner.Assert("tracked block", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Block.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        // Combat start only: a new turn must not add anything.
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("end turn 1", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("no further block on turn 2", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Block.IntValue ?? -1;
            return new TestResult(Amount == expected, $"expected still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// FakeAnchor: gains block at combat start (weaker variant)
[HarmonyPatch(typeof(FakeAnchor), nameof(FakeAnchor.BeforeCombatStart))]
public sealed class FakeAnchorStats : SimpleCounterStats<FakeAnchor>
{
    public override string Format => "Provided {0} base [gold]Block[/gold].";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(FakeAnchor __instance) =>
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight("NIBBITS_WEAK"));
        runner.WaitFor(GameEvent.CombatStart);
        runner.Assert("tracked block", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Block.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        // Combat start only: a new turn must not add anything.
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("end turn 1", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("no further block on turn 2", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Block.IntValue ?? -1;
            return new TestResult(Amount == expected, $"expected still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// CloakClasp: gains block per card in hand at turn end
[HarmonyPatch(typeof(CloakClasp), nameof(CloakClasp.BeforeSideTurnEnd))]
public sealed class CloakClaspStats : SimpleCounterStats<CloakClasp>
{
    public override string Format => "Provided {0} base [gold]Block[/gold].";
    public static void Postfix(CloakClasp __instance, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(__instance.Owner.Creature)) return;
        var cards = PileType.Hand.GetPile(__instance.Owner).Cards;
        if (cards.Count == 0) return;
        int block = (int)((decimal)cards.Count * __instance.DynamicVars.Block.BaseValue);
        Track(__instance, s => s.Amount += block);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight("NIBBITS_WEAK"));
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("spawn cards and end turn", () => {
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.EndTurn();
        });
        runner.WaitFor(GameEvent.TurnEnd);
        runner.Assert("tracked block", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var blockPerCard = (int)relic!.DynamicVars.Block.BaseValue;
            var expected = 3 * blockPerCard;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// IntimidatingHelmet: gains block when playing high-cost cards
[HarmonyPatch(typeof(IntimidatingHelmet), nameof(IntimidatingHelmet.BeforeCardPlayed))]
public sealed class IntimidatingHelmetStats : SimpleCounterStats<IntimidatingHelmet>
{
    public override string Format => "Provided {0} base [gold]Block[/gold].";
    public static void Postfix(IntimidatingHelmet __instance, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != __instance.Owner) return;
        if (cardPlay.Resources.EnergyValue < __instance.DynamicVars.Energy.IntValue) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight("NIBBITS_WEAK"));
        runner.WaitFor(GameEvent.PlayerTurnStart);
        // A 1-cost card is below the Energy threshold (2) and must not count.
        runner.Do("play cheap card", () => {
            TestHelpers.AddEnergy(10);
            TestHelpers.EnableGodMode();
            TestHelpers.ProtectEnemy();
            TestHelpers.SpawnCard("STRIKE_IRONCLAD");
            TestHelpers.PlayCard(0, 0);
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("cheap card does not count", () =>
            new TestResult(Amount == 0, $"expected 0 after a 1-cost card, got {Amount}"));
        runner.Do("play high-cost card", () => {
            TestHelpers.SpawnCard("BLUDGEON");
            TestHelpers.PlayThenEndTurn(1, 0);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked block", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Block.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// Regalite: gains block the first time each turn the owner generates a card for combat.
// The game guards on its private _usedThisTurn (set before the block gain, reset in
// BeforeSideTurnStart and AfterCombatEnd), so every generated card after the first in a
// turn must not count: capture the flag before the call and count only when it was clear.
[HarmonyPatch(typeof(Regalite), nameof(Regalite.AfterCardGeneratedForCombat))]
public sealed class RegaliteStats : SimpleCounterStats<Regalite>
{
    private static readonly FieldInfo UsedField =
        AccessTools.Field(typeof(Regalite), "_usedThisTurn");

    public override string Format => "Provided {0} base [gold]Block[/gold].";

    public static void Prefix(Regalite __instance, out bool __state) =>
        __state = (bool)UsedField.GetValue(__instance)!;

    public static void Postfix(Regalite __instance, bool __state, CardModel card, Player? creator)
    {
        if (__state) return; // already used this turn
        if (creator == null || creator != __instance.Owner) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // Blade Dance generates 3 Shivs through CardPileCmd.AddGeneratedCardsToCombat, which
        // fires AfterCardGeneratedForCombat per Shiv; only the first per turn gains block.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight("NIBBITS_WEAK"));
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("play Blade Dance (3 shivs)", () => {
            TestHelpers.AddEnergy(10);
            TestHelpers.EnableGodMode();
            TestHelpers.ProtectEnemy();
            TestHelpers.SpawnCard("BLADE_DANCE");
            TestHelpers.PlayCard(0);
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("one block gain for three generated shivs", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Block.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        // Play the shivs (they exhaust) so turn 2 starts with an empty hand instead of redrawing them.
        runner.Do("play the 3 shivs and end turn 1", () => TestHelpers.PlayThenEndTurn(3, 0));
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        // Turn 2: two Blade Dances (6 shivs) in one turn gain block once more, not six times.
        runner.Do("play two Blade Dances on turn 2", () => {
            TestHelpers.AddEnergy(10);
            TestHelpers.SpawnCard("BLADE_DANCE");
            TestHelpers.SpawnCard("BLADE_DANCE");
            TestHelpers.PlayThenEndTurn(2);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("once per turn on turn 2", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = 2 * (relic?.DynamicVars.Block.IntValue ?? -1);
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => {
            TestHelpers.RemoveRelic(RelicId);
            Reset();
        });
    }
#endif
}

// SelfFormingClay: applies block-next-turn power after taking unblocked damage
[HarmonyPatch(typeof(SelfFormingClay), nameof(SelfFormingClay.AfterDamageReceived))]
public sealed class SelfFormingClayStats : SimpleCounterStats<SelfFormingClay>
{
    public override string Format => "Queued {0} base [gold]Block[/gold] for next turn.";
    public static void Postfix(SelfFormingClay __instance, Creature target, DamageResult result)
    {
        if (!CombatManager.Instance.IsInProgress) return;
        if (target != __instance.Owner.Creature) return;
        if (result.UnblockedDamage <= 0) return;
        Track(__instance, s => s.Amount += (int)__instance.DynamicVars["BlockNextTurn"].BaseValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight("NIBBITS_WEAK"));
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("end turn to let enemy attack", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.DamageReceived);
        runner.Assert("tracked block", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = (int)(relic?.DynamicVars["BlockNextTurn"].BaseValue ?? -1);
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        // Only unblocked damage counts: a fully blocked enemy turn must not add anything.
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Do("block everything and end turn 2", () => {
            TestHelpers.GiveBlock(999);
            TestHelpers.EndTurn();
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("blocked damage does not count", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = (int)(relic?.DynamicVars["BlockNextTurn"].BaseValue ?? -1);
            return new TestResult(Amount == expected, $"expected still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// BoneFlute: gains block when pet (Osty) attacks
[HarmonyPatch(typeof(BoneFlute), nameof(BoneFlute.AfterAttack))]
public sealed class BoneFluteStats : SimpleCounterStats<BoneFlute>
{
    public override string Format => "Provided {0} base [gold]Block[/gold].";
    public static void Postfix(BoneFlute __instance, AttackCommand command)
    {
        if (command.Attacker?.Monster is not MegaCrit.Sts2.Core.Models.Monsters.Osty) return;
        if (command.Attacker.PetOwner?.Creature != __instance.Owner.Creature) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // Bodyguard summons Osty (OstyCmd.Summon), Poke attacks through DamageCmd.FromOsty so the
        // AttackCommand's Attacker is Osty; a Strike attacks as the player and must not count.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight("NIBBITS_WEAK"));
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("summon Osty with Bodyguard", () => {
            TestHelpers.AddEnergy(10);
            TestHelpers.ProtectEnemy();
            TestHelpers.SpawnCard("BODYGUARD");
            TestHelpers.PlayCard(0);
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Do("attack with Osty via Poke", () => {
            TestHelpers.SpawnCard("POKE");
            TestHelpers.PlayCard(0, 0);
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("tracked block from Osty attack", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Block.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Do("attack as the player with Strike", () => {
            TestHelpers.SpawnCard("STRIKE_IRONCLAD");
            TestHelpers.PlayCard(0, 0);
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("player attack does not count", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Block.IntValue ?? -1;
            return new TestResult(Amount == expected, $"expected still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// HornCleat: gains block when block is cleared on turn 2
[HarmonyPatch(typeof(HornCleat), nameof(HornCleat.AfterBlockCleared))]
public sealed class HornCleatStats : SimpleCounterStats<HornCleat>
{
    public override string Format => "Provided {0} base [gold]Block[/gold].";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(HornCleat __instance, Creature creature)
    {
        if (__instance.Owner.PlayerCombatState!.TurnNumber != 2) return;
        if (creature != __instance.Owner.Creature) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight("NIBBITS_WEAK"));
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("nothing on turn 1", () =>
            new TestResult(Amount == 0, $"expected 0 before turn 2, got {Amount}"));
        // Turn 1: give block so AfterBlockCleared fires at start of turn 2
        runner.Do("give block and end turn 1", () => {
            TestHelpers.GiveBlock(10);
            TestHelpers.EndTurn();
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000); // turn 2 — block cleared triggers here
        runner.Assert("tracked block", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Block.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        // Turn 2 only: block cleared at the start of turn 3 must not count.
        runner.Do("give block and end turn 2", () => {
            TestHelpers.GiveBlock(10);
            TestHelpers.EndTurn();
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000); // turn 3
        runner.Assert("no further block on turn 3", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Block.IntValue ?? -1;
            return new TestResult(Amount == expected, $"expected still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// Orichalcum: gains block at turn end if no block
[HarmonyPatch(typeof(Orichalcum), nameof(Orichalcum.BeforeSideTurnEnd))]
public sealed class OrichalcumStats : SimpleCounterStats<Orichalcum>
{
    private static readonly FieldInfo TriggerField = AccessTools.Field(typeof(Orichalcum), "_shouldTrigger");
    public override string Format => "Provided {0} base [gold]Block[/gold].";
    public static void Prefix(Orichalcum __instance, out bool __state) =>
        __state = (bool)TriggerField.GetValue(__instance)!;
    public static void Postfix(Orichalcum __instance, bool __state)
    {
        if (!__state) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic and plating", () => { TestHelpers.AddRelic(RelicId); TestHelpers.AddRelic("GORGET"); });
        runner.Do("start fight", () => TestHelpers.StartFight("NIBBITS_WEAK"));
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("end turn with no block", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.TurnEnd);
        runner.Assert("tracked block", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Block.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        // Only with no block: ending turn 2 while holding block must not count.
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Do("end turn 2 with block", () => {
            TestHelpers.GiveBlock(10);
            TestHelpers.EndTurn();
        });
        runner.WaitFor(GameEvent.TurnEnd, 15000);
        runner.Assert("no block gain while holding block", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Block.IntValue ?? -1;
            return new TestResult(Amount == expected, $"expected still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); TestHelpers.RemoveRelic("GORGET"); Reset(); });
    }
#endif
}

// FakeOrichalcum: gains block at turn end if no block (weaker variant)
[HarmonyPatch(typeof(FakeOrichalcum), nameof(FakeOrichalcum.BeforeSideTurnEnd))]
public sealed class FakeOrichalcumStats : SimpleCounterStats<FakeOrichalcum>
{
    private static readonly FieldInfo TriggerField = AccessTools.Field(typeof(FakeOrichalcum), "_shouldTrigger");
    public override string Format => "Provided {0} base [gold]Block[/gold].";
    public static void Prefix(FakeOrichalcum __instance, out bool __state) =>
        __state = (bool)TriggerField.GetValue(__instance)!;
    public static void Postfix(FakeOrichalcum __instance, bool __state)
    {
        if (!__state) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic and plating", () => { TestHelpers.AddRelic(RelicId); TestHelpers.AddRelic("GORGET"); });
        runner.Do("start fight", () => TestHelpers.StartFight("NIBBITS_WEAK"));
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("end turn with no block", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.TurnEnd);
        runner.Assert("tracked block", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Block.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        // Only with no block: ending turn 2 while holding block must not count.
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Do("end turn 2 with block", () => {
            TestHelpers.GiveBlock(10);
            TestHelpers.EndTurn();
        });
        runner.WaitFor(GameEvent.TurnEnd, 15000);
        runner.Assert("no block gain while holding block", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Block.IntValue ?? -1;
            return new TestResult(Amount == expected, $"expected still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); TestHelpers.RemoveRelic("GORGET"); Reset(); });
    }
#endif
}

// ToughBandages: gains block when cards are discarded
[HarmonyPatch(typeof(ToughBandages), nameof(ToughBandages.AfterCardDiscarded))]
public sealed class ToughBandagesStats : SimpleCounterStats<ToughBandages>
{
    public override string Format => "Provided {0} base [gold]Block[/gold].";
    public static void Postfix(ToughBandages __instance, CardModel card)
    {
        if (card.Owner != __instance.Owner) return;
        if (__instance.Owner.Creature.Side != __instance.Owner.Creature.CombatState!.CurrentSide) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight("NIBBITS_WEAK"));
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("discard a card", () => {
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.DiscardCard(0);
        });
        runner.WaitFor(GameEvent.CardDiscarded);
        runner.Assert("tracked block", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Block.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// Gorget: applies Plating power at combat start
[HarmonyPatch(typeof(Gorget), nameof(Gorget.AfterRoomEntered))]
public sealed class GorgetStats : SimpleCounterStats<Gorget>
{
    public override string Format => "Gained {0} [gold]Plating[/gold].";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(Gorget __instance, AbstractRoom room)
    {
        if (room is not CombatRoom) return;
        Track(__instance, s => s.Amount += (int)__instance.DynamicVars["PlatingPower"].BaseValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight("NIBBITS_WEAK"));
        runner.WaitFor(GameEvent.CombatStart);
        runner.Assert("tracked plating", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = (int)(relic?.DynamicVars["PlatingPower"].BaseValue ?? -1);
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        // Combat rooms only: entering a rest site must not count.
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("enter rest site", () => TestHelpers.EnterRestSite());
        runner.WaitFor(GameEvent.RoomEntered, 15000);
        runner.Assert("non-combat room does not count", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = (int)(relic?.DynamicVars["PlatingPower"].BaseValue ?? -1);
            return new TestResult(Amount == expected, $"expected still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// OrnamentalFan: gains block every N attacks played
[HarmonyPatch(typeof(OrnamentalFan), nameof(OrnamentalFan.AfterCardPlayed))]
public sealed class OrnamentalFanStats : SimpleCounterStats<OrnamentalFan>
{
    private static readonly System.Reflection.FieldInfo AttacksField =
        AccessTools.Field(typeof(OrnamentalFan), "_attacksPlayedThisTurn");

    public override string Format => "Provided {0} base [gold]Block[/gold].";
    public static void Postfix(OrnamentalFan __instance, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != __instance.Owner) return;
        if (cardPlay.Card.Type != CardType.Attack) return;
        int attacks = (int)AttacksField.GetValue(__instance)!;
        int threshold = __instance.DynamicVars.Cards.IntValue;
#if DEBUG
        if (RelicStats.Core.Testing.TestManager.IsRunning)
            MainFile.Logger.Info($"[OrnamentalFan Postfix] attacks={attacks} threshold={threshold} Block={__instance.DynamicVars.Block.IntValue}");
#endif
        if (attacks % threshold != 0) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight("NIBBITS_WEAK"));
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("skip turn 1", () => { TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Do("play 3 shivs on turn 2", () => {
            for (int i = 0; i < 3; i++) TestHelpers.SpawnCard("SHIV");
            TestHelpers.PlayThenEndTurn(3, 0);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked block", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Block.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        // Every 3 attacks per turn: two attacks on turn 3 stay one short of the threshold.
        runner.Do("play 2 shivs on turn 3", () => {
            for (int i = 0; i < 2; i++) TestHelpers.SpawnCard("SHIV");
            TestHelpers.PlayThenEndTurn(2, 0);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("one short of the threshold does not count", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Block.IntValue ?? -1;
            return new TestResult(Amount == expected, $"expected still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// TheAbacus: gains block on shuffle
[HarmonyPatch(typeof(TheAbacus), nameof(TheAbacus.AfterShuffle))]
public sealed class TheAbacusStats : SimpleCounterStats<TheAbacus>
{
    public override string Format => "Provided {0} base [gold]Block[/gold].";
    public static void Postfix(TheAbacus __instance, Player shuffler)
    {
        if (shuffler != __instance.Owner) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        int snapshot = 0;
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight("NIBBITS_WEAK"));
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("snapshot and trigger shuffle", () => { snapshot = Amount; TestHelpers.TriggerShuffle(); });
        runner.WaitFor(GameEvent.Shuffle);
        runner.Assert("tracked block", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Block.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount - snapshot == expected, $"expected delta {expected}, got {Amount - snapshot} (was {snapshot})");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// CaptainsWheel: gains block when block is cleared on round 3
[HarmonyPatch(typeof(CaptainsWheel), nameof(CaptainsWheel.AfterBlockCleared))]
public sealed class CaptainsWheelStats : SimpleCounterStats<CaptainsWheel>
{
    public override string Format => "Provided {0} base [gold]Block[/gold].";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(CaptainsWheel __instance, Creature creature)
    {
        if (__instance.Owner.PlayerCombatState!.TurnNumber != 3) return;
        if (creature != __instance.Owner.Creature) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight("NIBBITS_WEAK"));
        runner.WaitFor(GameEvent.PlayerTurnStart);
        // Turn 1: end turn with block, so the turn-2 block clear proves the turn guard
        runner.Do("end turn 1", () => { TestHelpers.GiveBlock(10); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000); // turn 2
        runner.Assert("block cleared on turn 2 does not count", () =>
            new TestResult(Amount == 0, $"expected 0 before turn 3, got {Amount}"));
        // Turn 2: give block so AfterBlockCleared fires at start of turn 3
        runner.Do("give block and end turn 2", () => {
            TestHelpers.GiveBlock(10);
            TestHelpers.EndTurn();
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000); // turn 3 — block cleared triggers here
        runner.Assert("tracked block", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Block.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// Sai: gains block each turn
[HarmonyPatch(typeof(Sai), nameof(Sai.AfterSideTurnStart))]
public sealed class SaiStats : SimpleCounterStats<Sai>
{
    public override string Format => "Provided {0} base [gold]Block[/gold].";
    public static void Postfix(Sai __instance, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(__instance.Owner.Creature)) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight("NIBBITS_WEAK"));
        runner.WaitFor(GameEvent.SideTurnStart);
        runner.Assert("tracked block", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Block.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// DaughterOfTheWind: gains block when attacks are played
[HarmonyPatch(typeof(DaughterOfTheWind), nameof(DaughterOfTheWind.AfterCardPlayed))]
public sealed class DaughterOfTheWindStats : SimpleCounterStats<DaughterOfTheWind>
{
    public override string Format => "Provided {0} base [gold]Block[/gold].";
    public static void Postfix(DaughterOfTheWind __instance, CardPlay cardPlay)
    {
        if (cardPlay.Card.Type != CardType.Attack) return;
        if (cardPlay.Card.Owner != __instance.Owner) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight("NIBBITS_WEAK"));
        runner.WaitFor(GameEvent.PlayerTurnStart);
        // Attacks only: a Skill must not count.
        runner.Do("play skill", () => {
            TestHelpers.AddEnergy(10);
            TestHelpers.EnableGodMode();
            TestHelpers.ProtectEnemy();
            TestHelpers.SpawnCard("DEFEND_IRONCLAD");
            TestHelpers.PlayCard(0);
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("skill does not count", () =>
            new TestResult(Amount == 0, $"expected 0 after a skill, got {Amount}"));
        runner.Do("play attack", () => {
            TestHelpers.SpawnCard("STRIKE_IRONCLAD");
            TestHelpers.PlayThenEndTurn(1, 0);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked block", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Block.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// RippleBasin: gains block at turn end if no attacks played
[HarmonyPatch(typeof(RippleBasin), nameof(RippleBasin.BeforeSideTurnEnd))]
public sealed class RippleBasinStats : SimpleCounterStats<RippleBasin>
{
    public override string Format => "Provided {0} base [gold]Block[/gold].";
    public static void Postfix(RippleBasin __instance, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(__instance.Owner.Creature)) return;
        if (CombatManager.Instance.History.CardPlaysFinished.Any(
            (CardPlayFinishedEntry e) =>
                e.HappenedThisTurn(__instance.Owner.Creature.CombatState) &&
                e.CardPlay.Card.Type == CardType.Attack &&
                e.CardPlay.Card.Owner == __instance.Owner)) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        int snapshot = 0;
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight("NIBBITS_WEAK"));
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("snapshot and end turn", () => { snapshot = Amount; TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.TurnEnd);
        runner.Assert("tracked block", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Block.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount - snapshot == expected, $"expected delta {expected}, got {Amount - snapshot} (was {snapshot})");
        });
        // Only turns without an attack: playing a Strike on turn 2 must not count.
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Do("play attack and end turn 2", () => {
            snapshot = Amount;
            TestHelpers.AddEnergy(10);
            TestHelpers.ProtectEnemy();
            TestHelpers.SpawnCard("STRIKE_IRONCLAD");
            TestHelpers.PlayThenEndTurn(1, 0);
        });
        runner.WaitFor(GameEvent.TurnEnd, 15000);
        runner.Assert("no block gain after an attack", () =>
            new TestResult(Amount == snapshot, $"expected still {snapshot}, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// TuningFork: gains block every N (DynamicVars.Cards, 10) skills played, counted across turns.
// The game does SkillsPlayed++ and gains block when the counter reaches the threshold, then
// subtracts the threshold. Its _isActivating flag is only a 1-second visual window, so instead
// capture the public SkillsPlayed before the call: the block is gained exactly when the
// incremented value reaches the threshold.
[HarmonyPatch(typeof(TuningFork), nameof(TuningFork.AfterCardPlayed))]
public sealed class TuningForkStats : SimpleCounterStats<TuningFork>
{
    public override string Format => "Provided {0} base [gold]Block[/gold].";

    public static void Prefix(TuningFork __instance, out int __state) =>
        __state = __instance.SkillsPlayed;

    public static void Postfix(TuningFork __instance, int __state, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != __instance.Owner) return;
        if (cardPlay.Card.Type != CardType.Skill) return;
        if (__state + 1 < __instance.DynamicVars.Cards.IntValue) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // Advance the live relic's counter to one short of the threshold through its public
        // NotifySkillPlayed, then the next skill triggers the block and the one after does not.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight("NIBBITS_WEAK"));
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("count skills up to one short of the threshold", () => {
            TestHelpers.AddEnergy(10);
            TestHelpers.EnableGodMode();
            TestHelpers.ProtectEnemy();
            var relic = TestHelpers.GetRelic<TuningFork>()!;
            int threshold = relic.DynamicVars.Cards.IntValue;
            for (int i = 0; i < threshold - 1; i++) relic.NotifySkillPlayed();
            TestHelpers.SpawnCard("DEFEND_IRONCLAD");
            TestHelpers.PlayCard(0);
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("tracked block on the threshold skill", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Block.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Do("play one more skill", () => {
            TestHelpers.SpawnCard("DEFEND_IRONCLAD");
            TestHelpers.PlayThenEndTurn(1);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("skill after the threshold does not count", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Block.IntValue ?? -1;
            return new TestResult(Amount == expected, $"expected still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// Vambrace: doubles the Block of the first card that gains Block each combat.
// ModifyBlockMultiplicative is a pure calculation the game also runs for card
// previews and hover text, so counting its result overcounts. The relic marks
// itself used in AfterCardPlayed, once the doubled card has resolved, so count
// that false -> true flip instead: at most once per combat.
[HarmonyPatch(typeof(Vambrace), nameof(Vambrace.AfterCardPlayed))]
public sealed class VambraceStats : SimpleCounterStats<Vambrace>
{
    private static readonly FieldInfo UsedField =
        AccessTools.Field(typeof(Vambrace), "_blockGainedThisCombat");

    public override string Format => "Doubled first [gold]Block[/gold] {0} times.";
    public override StatCadence Cadence => StatCadence.Total;

    public static void Prefix(Vambrace __instance, out bool __state) =>
        __state = (bool)UsedField.GetValue(__instance)!;

    public static void Postfix(Vambrace __instance, bool __state)
    {
        if (__state) return; // already used earlier this combat
        if (!(bool)UsedField.GetValue(__instance)!) return; // this card wasn't the doubled one
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // Only the first Block card of the combat is doubled. Play two Defends on
        // turn 1 and another on turn 2: the count must be exactly 1, however many
        // times the game previews block values along the way.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight("NIBBITS_WEAK"));
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("play two block cards", () => {
            TestHelpers.AddEnergy(10);
            TestHelpers.EnableGodMode();
            TestHelpers.ProtectEnemy();
            TestHelpers.SpawnCard("DEFEND");
            TestHelpers.SpawnCard("DEFEND");
            TestHelpers.PlayThenEndTurn(2);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("doubled exactly once on turn 1", () =>
            new TestResult(Amount == 1, $"expected 1 doubling, got {Amount}"));
        runner.Do("play block card on turn 2", () => {
            TestHelpers.AddEnergy(10);
            TestHelpers.SpawnCard("DEFEND");
            TestHelpers.PlayThenEndTurn(1);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("no further doubling on turn 2", () =>
            new TestResult(Amount == 1, $"expected still 1 doubling, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// Permafrost: gains block from the first Power played each combat.
// The game awaits CreatureCmd.GainBlock BEFORE setting _activatedThisCombat, and GainBlock
// yields, so the Postfix runs while the flag is still false. The block is gained under exactly
// the guards below whenever the flag was clear going in, so count on the captured flag alone.
[HarmonyPatch(typeof(Permafrost), nameof(Permafrost.AfterCardPlayed))]
public sealed class PermafrostStats : SimpleCounterStats<Permafrost>
{
    private static readonly FieldInfo ActivatedField =
        AccessTools.Field(typeof(Permafrost), "_activatedThisCombat");

    public override string Format => "Provided {0} base [gold]Block[/gold].";
    public override StatCadence Cadence => StatCadence.Total;

    public static void Prefix(Permafrost __instance, out bool __state) =>
        __state = (bool)ActivatedField.GetValue(__instance)!;

    public static void Postfix(Permafrost __instance, bool __state, CardPlay cardPlay)
    {
        if (__state) return; // already activated earlier this combat
        if (!CombatManager.Instance.IsInProgress) return;
        if (cardPlay.Card.Owner != __instance.Owner) return;
        if (cardPlay.Card.Type != CardType.Power) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // The first Power (Demon Form) gains block; a second Power (Inflame) in the same combat does not.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight("NIBBITS_WEAK"));
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("play power card", () => {
            TestHelpers.AddEnergy(10);
            TestHelpers.EnableGodMode();
            TestHelpers.ProtectEnemy();
            TestHelpers.SpawnCard("DEMON_FORM");
            TestHelpers.PlayThenEndTurn(1);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked block from first power", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Block.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Do("play a second power on turn 2", () => {
            TestHelpers.AddEnergy(10);
            TestHelpers.SpawnCard("INFLAME");
            TestHelpers.PlayThenEndTurn(1);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("second power in the combat does not count", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Block.IntValue ?? -1;
            return new TestResult(Amount == expected, $"expected still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}
