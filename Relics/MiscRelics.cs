using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using RelicStats.Core;
#if DEBUG
using RelicStats.Core.Testing;
#endif

namespace RelicStats.Relics;

// BlackStar: extra relic reward from elites
[HarmonyPatch(typeof(BlackStar), nameof(BlackStar.TryModifyRewards))]
public sealed class BlackStarStats : SimpleCounterStats<BlackStar>
{
    public override string Format => "Gained {0} extra relic rewards.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(BlackStar __instance, bool __result)
    {
        if (!__result) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // TryModifyRewards runs inside room-end reward generation (RewardsSet.GenerateWithoutOffering ->
        // Hook.ModifyRewards), about a second after CombatVictory, so RewardsGenerated is the sync point.
        // Its room guard (RoomType.Elite) has no cheap negative: a Monster-room win needs a second fight.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start elite fight", () => TestHelpers.StartEliteFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("nothing before the rewards exist", () =>
            new TestResult(Amount == 0, $"expected Amount == 0, got {Amount}"));
        runner.Do("win combat", () => TestHelpers.WinCombat());
        runner.WaitFor(GameEvent.RewardsGenerated, 15000);
        runner.Assert("tracked one extra relic reward", () =>
            new TestResult(Amount == 1, $"expected Amount == 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// BiiigHug: adds soot cards on shuffle
[HarmonyPatch(typeof(BiiigHug), nameof(BiiigHug.AfterShuffle))]
public sealed class BiiigHugStats : SimpleCounterStats<BiiigHug>
{
    public override string Format => "Added {0} soot cards.";
    public static void Postfix(BiiigHug __instance, Player shuffler)
    {
        if (shuffler != __instance.Owner) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("trigger shuffle", () => TestHelpers.TriggerShuffle());
        runner.WaitFor(GameEvent.Shuffle);
        runner.Assert("tracked soot cards", () =>
            new TestResult(Amount == 1, $"expected Amount == 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// BurningSticks: duplicates first exhausted skill per combat
[HarmonyPatch(typeof(BurningSticks), nameof(BurningSticks.AfterCardExhausted))]
public sealed class BurningSticksStats : SimpleCounterStats<BurningSticks>
{
    public override string Format => "Duplicated {0} cards.";
    public override StatCadence Cadence => StatCadence.Total;
    private static readonly FieldInfo _wasUsedField =
        AccessTools.Field(typeof(BurningSticks), "_wasUsedThisCombat");
    private static bool _wasUnusedBeforeCall;

    public static void Prefix(BurningSticks __instance, CardModel card)
    {
        _wasUnusedBeforeCall = card.Owner == __instance.Owner
            && card.Type == CardType.Skill
            && !(bool)_wasUsedField.GetValue(__instance)!;
    }

    public static void Postfix(BurningSticks __instance, CardModel card)
    {
        if (!_wasUnusedBeforeCall) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("exhaust a skill", () => {
            TestHelpers.SpawnCard("DEFEND");
            TestHelpers.ExhaustCard();
        });
        runner.WaitFor(GameEvent.CardExhausted);
        runner.Assert("tracked duplication", () =>
            new TestResult(Amount == 1, $"expected Amount == 1, got {Amount}"));
        // _wasUsedThisCombat is set after the first duplication: a second exhausted skill is not cloned.
        runner.Do("exhaust a second skill", () => {
            TestHelpers.SpawnCard("DEFEND");
            TestHelpers.ExhaustCard();
        });
        runner.WaitFor(GameEvent.CardExhausted);
        runner.Assert("second exhausted skill is not duplicated (once per combat)", () =>
            new TestResult(Amount == 1, $"expected Amount still 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// ChemicalX: increases X values by 2
[HarmonyPatch(typeof(ChemicalX), nameof(ChemicalX.ModifyXValue))]
public sealed class ChemicalXStats : SimpleCounterStats<ChemicalX>
{
    public override string Format => "Added {0} to X values.";
    public static void Postfix(ChemicalX __instance, CardModel card, int __result, int originalValue)
    {
        if (__instance.Owner != card.Owner) return;
        int increase = __result - originalValue;
        if (increase <= 0) return;
        Track(__instance, s => s.Amount += increase);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // ModifyXValue is only reached from CardModel.ResolveEnergyXValue, i.e. once per X-cost card
        // play. Malaise is an X-cost skill targeting an enemy; ProtectEnemy keeps the Nibbit alive
        // through the -X Strength / X Weak it applies.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("play an X-cost card", () => {
            TestHelpers.ProtectEnemy();
            TestHelpers.AddEnergy(10);
            TestHelpers.SpawnCard("MALAISE");
            TestHelpers.PlayCard(0, 0);
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("tracked X increase", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars["Increase"].IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        // An X-cost play spends all energy, so refill before the non-X card.
        runner.Do("play a non-X card", () => {
            TestHelpers.AddEnergy(10);
            TestHelpers.SpawnCard("STRIKE_IRONCLAD");
            TestHelpers.PlayCard(0, 0);
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("non-X card does not add", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars["Increase"].IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// CrackedCore: channels lightning at combat start
[HarmonyPatch(typeof(CrackedCore), nameof(CrackedCore.BeforeSideTurnStart))]
public sealed class CrackedCoreStats : SimpleCounterStats<CrackedCore>
{
    public override string Format => "Channeled {0} [gold]Lightning[/gold] orbs.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(CrackedCore __instance, CombatSide side, ICombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (__instance.Owner.PlayerCombatState!.TurnNumber > 1) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars["Lightning"].IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.SideTurnStart);
        runner.Assert("tracked lightning orbs", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars["Lightning"].IntValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected Amount == {expected}, got {Amount}");
        });
        // Turn-1 guard: turn 2's side-turn start (after its PlayerTurnStart) must not channel again.
        runner.Do("end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.WaitFor(GameEvent.SideTurnStart);
        runner.Assert("turn 2 does not channel again", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars["Lightning"].IntValue;
            return new TestResult(Amount == expected, $"expected Amount still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// InfusedCore: channels lightning at combat start (upgraded variant)
[HarmonyPatch(typeof(InfusedCore), nameof(InfusedCore.AfterSideTurnStart))]
public sealed class InfusedCoreStats : SimpleCounterStats<InfusedCore>
{
    public override string Format => "Channeled {0} [gold]Lightning[/gold] orbs.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(InfusedCore __instance, CombatSide side, ICombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (__instance.Owner.PlayerCombatState!.TurnNumber > 1) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars["Lightning"].IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.SideTurnStart);
        runner.Assert("tracked stat", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars["Lightning"].IntValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected Amount == {expected}, got {Amount}");
        });
        // Turn-1 guard: turn 2's side-turn start (after its PlayerTurnStart) must not channel again.
        runner.Do("end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.WaitFor(GameEvent.SideTurnStart);
        runner.Assert("turn 2 does not channel again", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars["Lightning"].IntValue;
            return new TestResult(Amount == expected, $"expected Amount still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// DelicateFrond: generates potions before combat
[HarmonyPatch(typeof(DelicateFrond), nameof(DelicateFrond.BeforeCombatStart))]
public sealed class DelicateFrondStats : SimpleCounterStats<DelicateFrond>
{
    public override string Format => "Generated potions {0} times.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(DelicateFrond __instance) =>
        Track(__instance, s => s.Amount++);

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.CombatStart);
        runner.Assert("tracked stat", () =>
            new TestResult(Amount == 1, $"expected Amount == 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// DivineRight: gains stars at combat start
[HarmonyPatch(typeof(DivineRight), nameof(DivineRight.AfterRoomEntered))]
public sealed class DivineRightStats : SimpleCounterStats<DivineRight>
{
    public override string Format => "Gained {0} [gold]Stars[/gold].";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(DivineRight __instance, AbstractRoom room)
    {
        if (room is not CombatRoom) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Stars.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        // Room guard: AfterRoomEntered for a non-combat room grants nothing.
        runner.Do("enter rest site", () => TestHelpers.EnterRestSite());
        runner.WaitFor(GameEvent.RoomEntered);
        runner.Assert("non-combat room grants nothing", () =>
            new TestResult(Amount == 0, $"expected Amount == 0, got {Amount}"));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.CombatStart);
        runner.Assert("tracked stars", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Stars.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// FresnelLens: enchants cards with Nimble
[HarmonyPatch(typeof(FresnelLens), nameof(FresnelLens.TryModifyCardBeingAddedToDeck))]
public sealed class FresnelLensStats : SimpleCounterStats<FresnelLens>
{
    public override string Format => "Enchanted {0} cards.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(FresnelLens __instance, bool __result)
    {
        if (!__result) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // TryModifyCardBeingAddedToDeck runs synchronously inside the deck add (CardPileCmd.Add ->
        // Hook.ModifyCardBeingAddedToDeck, before any await). Nimble.CanEnchant requires
        // card.GainsBlock, so a Defend is enchanted and a Strike is left alone.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("add a block card to deck", () => TestHelpers.AddCardToDeck("DEFEND_IRONCLAD"));
        runner.Assert("tracked enchantment", () =>
            new TestResult(Amount == 1, $"expected Amount == 1, got {Amount}"));
        runner.Do("add a non-block card to deck", () => TestHelpers.AddCardToDeck("STRIKE_IRONCLAD"));
        runner.Assert("non-block card is not enchanted", () =>
            new TestResult(Amount == 1, $"expected Amount still 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// LavaLamp: upgrades card rewards when no damage taken
[HarmonyPatch(typeof(LavaLamp), nameof(LavaLamp.TryModifyCardRewardOptionsLate))]
public sealed class LavaLampStats : SimpleCounterStats<LavaLamp>
{
    public override string Format => "Upgraded card rewards {0} times.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(LavaLamp __instance, bool __result)
    {
        if (!__result) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // The combat's one CardReward is populated (TryModifyCardRewardOptionsLate) inside
        // RewardsSet.GenerateWithoutOffering right before Hook.ModifyRewards, so RewardsGenerated is
        // the sync point. No damage is taken here, so the reward is upgraded exactly once. The
        // damaged-combat negative would need a second fight, so it is not covered.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("nothing before the rewards exist", () =>
            new TestResult(Amount == 0, $"expected Amount == 0, got {Amount}"));
        runner.Do("win combat", () => TestHelpers.WinCombat());
        runner.WaitFor(GameEvent.RewardsGenerated, 15000);
        runner.Assert("tracked one upgraded card reward", () =>
            new TestResult(Amount == 1, $"expected Amount == 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// LunarPastry: gains stars at end of each turn
[HarmonyPatch(typeof(LunarPastry), nameof(LunarPastry.AfterSideTurnEnd))]
public sealed class LunarPastryStats : SimpleCounterStats<LunarPastry>
{
    public override string Format => "Gained {0} [gold]Stars[/gold].";
    public static void Postfix(LunarPastry __instance, CombatSide side)
    {
        if (side != __instance.Owner.Creature.Side) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Stars.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Do("enable god mode + protect enemy + end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked stars", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Stars.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// MoltenEgg: auto-upgrades attack cards added to deck
[HarmonyPatch(typeof(MoltenEgg), nameof(MoltenEgg.TryModifyCardBeingAddedToDeck))]
public sealed class MoltenEggStats : SimpleCounterStats<MoltenEgg>
{
    public override string Format => "Upgraded {0} attack cards.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(MoltenEgg __instance, bool __result)
    {
        if (!__result) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // TryModifyCardBeingAddedToDeck runs synchronously inside the deck add and only accepts
        // upgradable Attacks, so a Strike counts and a Defend does not.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("add attack to deck", () => TestHelpers.AddCardToDeck("STRIKE_IRONCLAD"));
        runner.Assert("tracked attack card upgrade", () =>
            new TestResult(Amount == 1, $"expected Amount == 1, got {Amount}"));
        runner.Do("add skill to deck", () => TestHelpers.AddCardToDeck("DEFEND_IRONCLAD"));
        runner.Assert("skill is not upgraded", () =>
            new TestResult(Amount == 1, $"expected Amount still 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// ToxicEgg: auto-upgrades skill cards added to deck
[HarmonyPatch(typeof(ToxicEgg), nameof(ToxicEgg.TryModifyCardBeingAddedToDeck))]
public sealed class ToxicEggStats : SimpleCounterStats<ToxicEgg>
{
    public override string Format => "Upgraded {0} skill cards.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(ToxicEgg __instance, bool __result)
    {
        if (!__result) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // TryModifyCardBeingAddedToDeck runs synchronously inside the deck add and only accepts
        // upgradable Skills, so a Defend counts and a Strike does not.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("add skill to deck", () => TestHelpers.AddCardToDeck("DEFEND_IRONCLAD"));
        runner.Assert("tracked skill card upgrade", () =>
            new TestResult(Amount == 1, $"expected Amount == 1, got {Amount}"));
        runner.Do("add attack to deck", () => TestHelpers.AddCardToDeck("STRIKE_IRONCLAD"));
        runner.Assert("attack is not upgraded", () =>
            new TestResult(Amount == 1, $"expected Amount still 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// FrozenEgg: auto-upgrades power cards added to deck
[HarmonyPatch(typeof(FrozenEgg), nameof(FrozenEgg.TryModifyCardBeingAddedToDeck))]
public sealed class FrozenEggStats : SimpleCounterStats<FrozenEgg>
{
    public override string Format => "Upgraded {0} power cards.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(FrozenEgg __instance, bool __result)
    {
        if (!__result) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // TryModifyCardBeingAddedToDeck runs synchronously inside the deck add and only accepts
        // upgradable Powers, so Demon Form counts and a Strike does not.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("add power to deck", () => TestHelpers.AddCardToDeck("DEMON_FORM"));
        runner.Assert("tracked power card upgrade", () =>
            new TestResult(Amount == 1, $"expected Amount == 1, got {Amount}"));
        runner.Do("add attack to deck", () => TestHelpers.AddCardToDeck("STRIKE_IRONCLAD"));
        runner.Assert("attack is not upgraded", () =>
            new TestResult(Amount == 1, $"expected Amount still 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// MummifiedHand: makes a card free when playing powers
[HarmonyPatch(typeof(MummifiedHand), nameof(MummifiedHand.AfterCardPlayed))]
public sealed class MummifiedHandStats : SimpleCounterStats<MummifiedHand>
{
    public override string Format => "Made {0} cards free.";
    // Prefix: the relic is synchronous and sets its pick free, so afterwards the pick no longer costs
    // anything. It prefers hand cards that CostsEnergyOrStars(includeGlobalModifiers: true); its fallbacks
    // only pick cards that already cost nothing, so a card is only made free when such a card exists.
    public static void Prefix(MummifiedHand __instance, CardPlay cardPlay)
    {
        if (!CombatManager.Instance.IsInProgress) return;
        if (cardPlay.Card.Owner != __instance.Owner) return;
        if (cardPlay.Card.Type != CardType.Power) return;
        if (!PileType.Hand.GetPile(__instance.Owner).Cards.Any(c => c.CostsEnergyOrStars(includeGlobalModifiers: true))) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        // Negative: the Power is the only card, so the hand is empty when the relic runs.
        runner.Do("play a power with nothing else in hand", () => {
            TestHelpers.AddEnergy(10);
            TestHelpers.SpawnCard("DEMON_FORM");
            TestHelpers.PlayCard(TestHelpers.FindCardInHand(CardType.Power));
        });
        runner.WaitFor(GameEvent.CardPlayed, 15000);
        runner.Assert("no card to make free, nothing counted", () =>
            new TestResult(Amount == 0, $"expected Amount == 0, got {Amount}"));
        runner.Do("play a power with a Strike in hand", () => {
            TestHelpers.SpawnCard("STRIKE_IRONCLAD");
            TestHelpers.SpawnCard("DEMON_FORM");
            TestHelpers.PlayCard(TestHelpers.FindCardInHand(CardType.Power));
        });
        runner.WaitFor(GameEvent.CardPlayed, 15000);
        runner.Assert("tracked the Strike made free", () =>
        {
            var strike = PileType.Hand.GetPile(TestHelpers.Player!).Cards.FirstOrDefault(c => c.Id.Entry == "STRIKE_IRONCLAD");
            int cost = strike?.EnergyCost.GetWithModifiers(CostModifiers.All) ?? -1;
            return new TestResult(Amount == 1 && cost == 0, $"expected Amount == 1 and the Strike at cost 0, got Amount {Amount}, cost {cost}");
        });
        // Card-type guard: a Skill play is not a Power play, even with a costing Strike in hand.
        runner.Do("play a skill", () => {
            TestHelpers.SpawnCard("STRIKE_IRONCLAD");
            TestHelpers.SpawnCard("DEFEND_IRONCLAD");
            TestHelpers.PlayCard(TestHelpers.FindCardInHand(CardType.Skill));
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("skill play does not count", () =>
            new TestResult(Amount == 1, $"expected Amount still 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// PaelsEye: grants an extra turn if no cards played, exhausts hand
// Complex: tracks extra turns taken and cards exhausted
[HarmonyPatch]
public sealed class PaelsEyeStats : IRelicStats
{
    public string RelicId => RelicIdHelper.Slugify(nameof(PaelsEye));

    public int ExtraTurns { get; set; }
    public int CardsExhausted { get; set; }

    public string GetDescription(int effectiveTurns, int effectiveCombats)
    {
        return $"Took {Fmt.Blue(ExtraTurns)} extra turns.\n" +
               $"{Fmt.Gold("Exhausted")} {Fmt.Blue(CardsExhausted)} cards.";
    }

    public JsonObject Save()
    {
        var obj = new JsonObject
        {
            ["extraTurns"] = ExtraTurns,
            ["cardsExhausted"] = CardsExhausted,
        };
        return obj;
    }

    public void Load(JsonObject data)
    {
        ExtraTurns = data["extraTurns"]?.GetValue<int>() ?? 0;
        CardsExhausted = data["cardsExhausted"]?.GetValue<int>() ?? 0;
    }

    public void Reset()
    {
        ExtraTurns = 0;
        CardsExhausted = 0;
    }

    private static bool TryGet(PaelsEye instance, out PaelsEyeStats stats)
    {
        stats = null!;
        if (instance.IsMelted) return false;
        if (!LocalContext.IsMine(instance)) return false;
        if (RelicStatsRegistry.Get(RelicIdHelper.Slugify(nameof(PaelsEye))) is not PaelsEyeStats s) return false;
        stats = s;
        return true;
    }

    [HarmonyPatch(typeof(PaelsEye), nameof(PaelsEye.AfterTakingExtraTurn))]
    [HarmonyPostfix]
    public static void AfterTakingExtraTurnPostfix(PaelsEye __instance, Player player)
    {
        if (player != __instance.Owner) return;
        if (!TryGet(__instance, out var stats)) return;
        stats.ExtraTurns++;
    }

    [HarmonyPatch(typeof(PaelsEye), nameof(PaelsEye.BeforeSideTurnEndEarly))]
    [HarmonyPrefix]
    public static void BeforeSideTurnEndEarlyPrefix(PaelsEye __instance, IEnumerable<Creature> participants)
    {
        // Mirror the relic's exhaust guard: owner took part in the turn, relic unused this combat,
        // no (non-autoplay) cards played this turn, and owner was part of the last player turn.
        if (!participants.Contains(__instance.Owner.Creature)) return;
        var usedField = AccessTools.Field(typeof(PaelsEye), "_usedThisCombat");
        var wasPartField = AccessTools.Field(typeof(PaelsEye), "_wasOwnerPartOfLastPlayerTurn");
        var anyPlayedMethod = AccessTools.Method(typeof(PaelsEye), "AnyCardsPlayedThisTurn");
        if ((bool)usedField.GetValue(__instance)!) return;
        if ((bool)anyPlayedMethod.Invoke(__instance, null)!) return;
        if (!(bool)wasPartField.GetValue(__instance)!) return;
        if (!TryGet(__instance, out var stats)) return;
        var cards = CardPile.GetCards(__instance.Owner, PileType.Hand);
        stats.CardsExhausted += cards.Count();
    }



#if DEBUG
    public void RegisterTest(TestRunner runner)
    {
        // Ending a turn with cards in hand and nothing played: BeforeSideTurnEndEarly exhausts the
        // whole hand (CardsExhausted += hand size) and ShouldTakeExtraTurn grants the extra turn
        // (ExtraTurns++ in AfterTakingExtraTurn, which also sets _usedThisCombat). Both are guarded by
        // !UsedThisCombat && !AnyCardsPlayedThisTurn() && WasOwnerPartOfLastPlayerTurn, so the same
        // thing on the extra turn neither exhausts nor grants another turn.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("hold 2 cards, end turn without playing", () => {
            TestHelpers.DiscardHand();
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.SpawnCard("DEFEND");
            TestHelpers.EnableGodMode();
            TestHelpers.ProtectEnemy();
            TestHelpers.EndTurn();
        });
        // The next PlayerTurnStart is the extra turn (no enemy turn in between).
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("exhausted the hand and took the extra turn", () =>
            new TestResult(CardsExhausted == 2 && ExtraTurns == 1, $"expected CardsExhausted=2 ExtraTurns=1, got CardsExhausted={CardsExhausted} ExtraTurns={ExtraTurns}"));
        runner.Do("hold a card, end the extra turn without playing", () => {
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.EndTurn();
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("used up for the combat: no second exhaust or extra turn", () =>
            new TestResult(CardsExhausted == 2 && ExtraTurns == 1, $"expected CardsExhausted still 2 ExtraTurns still 1, got CardsExhausted={CardsExhausted} ExtraTurns={ExtraTurns}"));
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// PaelsWing: sacrifice card rewards for relics
[HarmonyPatch(typeof(PaelsWing), nameof(PaelsWing.OnSacrifice))]
public sealed class PaelsWingStats : SimpleCounterStats<PaelsWing>
{
    public override string Format => "Sacrificed {0} card rewards.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(PaelsWing __instance) =>
        Track(__instance, s => s.Amount++);

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // OnSacrifice is the public callback of the SACRIFICE card-reward alternative the relic adds in
        // TryModifyCardRewardAlternatives. It does RewardsSacrificed++ before its first await, so the
        // postfix has run by the time SacrificeCardReward returns. Generating a reward without picking
        // the alternative does not count.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("sacrifice a card reward", () => TestHelpers.SacrificeCardReward());
        runner.Assert("tracked one sacrifice", () =>
            new TestResult(Amount == 1, $"expected Amount == 1, got {Amount}"));
        runner.Do("generate a card reward without sacrificing", () => TestHelpers.GenerateCardReward());
        runner.Assert("plain card reward does not count", () =>
            new TestResult(Amount == 1, $"expected Amount still 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// PenNib: triggers every 10 attacks, doubling damage
// Complex: tracks trigger count and total attacks
[HarmonyPatch]
public sealed class PenNibStats : IRelicStats
{
    public string RelicId => RelicIdHelper.Slugify(nameof(PenNib));

    public int Triggers { get; set; }
    public int AttacksPlayed { get; set; }

    public string GetDescription(int effectiveTurns, int effectiveCombats)
    {
        return $"Doubled {Fmt.Gold("Damage")} {Fmt.Blue(Triggers)} times.\n" +
               $"Attacks played: {Fmt.Blue(AttacksPlayed)}";
    }

    public JsonObject Save()
    {
        var obj = new JsonObject
        {
            ["triggers"] = Triggers,
            ["attacksPlayed"] = AttacksPlayed,
        };
        return obj;
    }

    public void Load(JsonObject data)
    {
        Triggers = data["triggers"]?.GetValue<int>() ?? 0;
        AttacksPlayed = data["attacksPlayed"]?.GetValue<int>() ?? 0;
    }

    public void Reset()
    {
        Triggers = 0;
        AttacksPlayed = 0;
    }

    private static bool TryGet(PenNib instance, out PenNibStats stats)
    {
        stats = null!;
        if (instance.IsMelted) return false;
        if (!LocalContext.IsMine(instance)) return false;
        if (RelicStatsRegistry.Get(RelicIdHelper.Slugify(nameof(PenNib))) is not PenNibStats s) return false;
        stats = s;
        return true;
    }

    [HarmonyPatch(typeof(PenNib), nameof(PenNib.BeforeCardPlayed))]
    [HarmonyPostfix]
    public static void BeforeCardPlayedPostfix(PenNib __instance, CardPlay cardPlay)
    {
        if (cardPlay.Card.Type != CardType.Attack) return;
        if (cardPlay.Card.Owner != __instance.Owner) return;
        if (!TryGet(__instance, out var stats)) return;
        stats.AttacksPlayed++;
        // PenNib triggers when AttacksPlayed rolls over to 0 (mod 10)
        if (__instance.AttacksPlayed == 0)
        {
            stats.Triggers++;
        }
    }



#if DEBUG
    public void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        // BeforeCardPlayed counts every Attack; the relic's own counter wraps to 0 on the 10th, which
        // is the doubling. Shivs cost 0, so one turn plays all ten; each play is awaited on its own so
        // the 9th can be checked before the 10th. A Skill play afterwards changes neither counter.
        runner.Do("protect enemy + spawn 10 shivs", () => {
            TestHelpers.ProtectEnemy();
            for (int i = 0; i < 10; i++) TestHelpers.SpawnCard("SHIV");
        });
        for (int i = 1; i <= 9; i++)
        {
            runner.Do($"play shiv {i}", () => TestHelpers.PlayCard(0, 0));
            runner.WaitFor(GameEvent.CardPlayed);
        }
        runner.Assert("9 attacks, no doubling yet", () =>
            new TestResult(AttacksPlayed == 9 && Triggers == 0, $"expected AttacksPlayed=9 Triggers=0, got AttacksPlayed={AttacksPlayed} Triggers={Triggers}"));
        runner.Do("play shiv 10", () => TestHelpers.PlayCard(0, 0));
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("10 attacks, one doubling", () =>
            new TestResult(AttacksPlayed == 10 && Triggers == 1, $"expected AttacksPlayed=10 Triggers=1, got AttacksPlayed={AttacksPlayed} Triggers={Triggers}"));
        runner.Do("play a skill", () => {
            TestHelpers.SpawnCard("DEFEND_IRONCLAD");
            TestHelpers.PlayCard(0);
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("skills are not counted", () =>
            new TestResult(AttacksPlayed == 10 && Triggers == 1, $"expected AttacksPlayed still 10 Triggers still 1, got AttacksPlayed={AttacksPlayed} Triggers={Triggers}"));
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// PhylacteryUnbound: summons minions at combat start and each turn
// Complex: tracks combat start summons and turn summons separately
[HarmonyPatch]
public sealed class PhylacteryUnboundStats : IRelicStats
{
    public string RelicId => RelicIdHelper.Slugify(nameof(PhylacteryUnbound));

    public int CombatStartSummons { get; set; }
    public int TurnSummons { get; set; }

    public string GetDescription(int effectiveTurns, int effectiveCombats)
    {
        var total = CombatStartSummons + TurnSummons;
        return $"Summoned {Fmt.Blue(total)} minions.\n" +
               $"  At combat start: {Fmt.Blue(CombatStartSummons)}\n" +
               $"  Per turn: {Fmt.Blue(TurnSummons)}";
    }

    public JsonObject Save()
    {
        var obj = new JsonObject
        {
            ["combatStartSummons"] = CombatStartSummons,
            ["turnSummons"] = TurnSummons,
        };
        return obj;
    }

    public void Load(JsonObject data)
    {
        CombatStartSummons = data["combatStartSummons"]?.GetValue<int>() ?? 0;
        TurnSummons = data["turnSummons"]?.GetValue<int>() ?? 0;
    }

    public void Reset()
    {
        CombatStartSummons = 0;
        TurnSummons = 0;
    }

    private static bool TryGet(PhylacteryUnbound instance, out PhylacteryUnboundStats stats)
    {
        stats = null!;
        if (instance.IsMelted) return false;
        if (!LocalContext.IsMine(instance)) return false;
        if (RelicStatsRegistry.Get(RelicIdHelper.Slugify(nameof(PhylacteryUnbound))) is not PhylacteryUnboundStats s) return false;
        stats = s;
        return true;
    }

    [HarmonyPatch(typeof(PhylacteryUnbound), nameof(PhylacteryUnbound.BeforeCombatStart))]
    [HarmonyPostfix]
    public static void BeforeCombatStartPostfix(PhylacteryUnbound __instance)
    {
        if (!TryGet(__instance, out var stats)) return;
        stats.CombatStartSummons += __instance.DynamicVars["StartOfCombat"].IntValue;
    }

    [HarmonyPatch(typeof(PhylacteryUnbound), nameof(PhylacteryUnbound.AfterSideTurnStart))]
    [HarmonyPostfix]
    public static void AfterSideTurnStartPostfix(PhylacteryUnbound __instance, CombatSide side)
    {
        if (side != CombatSide.Player) return;
        if (!TryGet(__instance, out var stats)) return;
        stats.TurnSummons += __instance.DynamicVars["StartOfTurn"].IntValue;
    }



#if DEBUG
    public void RegisterTest(TestRunner runner)
    {
        // IRelicStats: tracks combat start summons (BeforeCombatStart) and turn summons (AfterSideTurnStart).
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.SideTurnStart);
        runner.Assert("tracked summons", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expectedStart = relic!.DynamicVars["StartOfCombat"].IntValue;
            var expectedTurn = relic!.DynamicVars["StartOfTurn"].IntValue;
            return new TestResult(
                CombatStartSummons == expectedStart && TurnSummons == expectedTurn,
                $"expected CombatStartSummons={expectedStart} TurnSummons={expectedTurn}, got CombatStartSummons={CombatStartSummons} TurnSummons={TurnSummons}");
        });
        // Turn 2: AfterSideTurnStart summons again (it runs after that turn's PlayerTurnStart, hence the
        // extra SideTurnStart wait); BeforeCombatStart does not repeat.
        runner.Do("end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.WaitFor(GameEvent.SideTurnStart);
        runner.Assert("turn 2 summons again, combat start does not repeat", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expectedStart = relic!.DynamicVars["StartOfCombat"].IntValue;
            var expectedTurn = relic!.DynamicVars["StartOfTurn"].IntValue * 2;
            return new TestResult(
                CombatStartSummons == expectedStart && TurnSummons == expectedTurn,
                $"expected CombatStartSummons still {expectedStart} TurnSummons={expectedTurn}, got CombatStartSummons={CombatStartSummons} TurnSummons={TurnSummons}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// PrayerWheel: extra card reward from normal combats
[HarmonyPatch(typeof(PrayerWheel), nameof(PrayerWheel.TryModifyRewards))]
public sealed class PrayerWheelStats : SimpleCounterStats<PrayerWheel>
{
    public override string Format => "Added {0} extra card rewards.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(PrayerWheel __instance, bool __result)
    {
        if (!__result) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // TryModifyRewards runs inside room-end reward generation (Hook.ModifyRewards), about a second
        // after CombatVictory, so RewardsGenerated is the sync point. Its room guard (RoomType.Monster)
        // has no cheap negative: an Elite-room win needs a second fight.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("nothing before the rewards exist", () =>
            new TestResult(Amount == 0, $"expected Amount == 0, got {Amount}"));
        runner.Do("win combat", () => TestHelpers.WinCombat());
        runner.WaitFor(GameEvent.RewardsGenerated, 15000);
        runner.Assert("tracked one extra card reward", () =>
            new TestResult(Amount == 1, $"expected Amount == 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// RazorTooth: upgrades skills and attacks when played
[HarmonyPatch(typeof(RazorTooth), nameof(RazorTooth.AfterCardPlayed))]
public sealed class RazorToothStats : SimpleCounterStats<RazorTooth>
{
    public override string Format => "Upgraded {0} cards.";
    private static bool _willUpgrade;

    public static void Prefix(RazorTooth __instance, CardPlay cardPlay)
    {
        _willUpgrade = false;
        if (cardPlay.Card.Owner != __instance.Owner) return;
        CardType type = cardPlay.Card.Type;
        if (type != CardType.Skill && type != CardType.Attack) return;
        if (!cardPlay.Card.IsUpgradable) return;
        _willUpgrade = true;
    }

    public static void Postfix(RazorTooth __instance)
    {
        if (!_willUpgrade) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("play card + end turn", () => {
            TestHelpers.AddEnergy(10);
            TestHelpers.EnableGodMode();
            TestHelpers.ProtectEnemy();
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.PlayThenEndTurn(1, 0);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked stat", () =>
            new TestResult(Amount == 1, $"expected Amount == 1, got {Amount}"));
        // Card-type guard: only Skills and Attacks are upgraded, a Power play (Inflame) is not.
        runner.Do("play a power", () => {
            TestHelpers.SpawnCard("INFLAME");
            // Turn 2's draw reshuffled turn 1's Strike back into the hand, so the Power is not at
            // index 0; look it up instead of assuming the hand holds only spawned cards.
            TestHelpers.PlayCard(TestHelpers.FindCardInHand(CardType.Power));
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("power play is not upgraded", () =>
            new TestResult(Amount == 1, $"expected Amount still 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// RedMask: applies weakness to all enemies at combat start
[HarmonyPatch(typeof(RedMask), nameof(RedMask.BeforeSideTurnStart))]
public sealed class RedMaskStats : SimpleCounterStats<RedMask>
{
    public override string Format => "Applied weakness {0} times.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(RedMask __instance, CombatSide side, ICombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (__instance.Owner.PlayerCombatState!.TurnNumber > 1) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked stat", () =>
            new TestResult(Amount == 1, $"expected Amount == 1, got {Amount}"));
        // Turn-1 guard: turn 2's side-turn start (after its PlayerTurnStart) must not apply again.
        runner.Do("end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.WaitFor(GameEvent.SideTurnStart);
        runner.Assert("turn 2 does not apply again", () =>
            new TestResult(Amount == 1, $"expected Amount still 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// RuinedHelmet: doubles first strength gain per combat
[HarmonyPatch(typeof(RuinedHelmet), nameof(RuinedHelmet.AfterModifyingPowerAmountReceived))]
public sealed class RuinedHelmetStats : SimpleCounterStats<RuinedHelmet>
{
    public override string Format => "Doubled strength {0} times.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(RuinedHelmet __instance) =>
        Track(__instance, s => s.Amount++);

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // TryModifyPowerAmountReceived doubles the first positive Strength gain of the combat and only
        // then puts the relic in the receivedModifiers list that AfterModifyingPowerAmountReceived
        // iterates; once _usedThisCombat is set it returns false and is skipped. No god mode: its
        // Strength grant would be that first gain.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("play a Strength card", () => {
            TestHelpers.ProtectEnemy();
            TestHelpers.AddEnergy(10);
            TestHelpers.SpawnCard("INFLAME");
            TestHelpers.PlayCard(0);
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("tracked the doubling", () =>
            new TestResult(Amount == 1, $"expected Amount == 1, got {Amount}"));
        runner.Do("play a second Strength card", () => {
            TestHelpers.SpawnCard("INFLAME");
            TestHelpers.PlayCard(0);
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("second Strength gain is not doubled (once per combat)", () =>
            new TestResult(Amount == 1, $"expected Amount still 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// Shovel: adds dig option at rest sites (track via TryModifyRestSiteOptions as proxy for availability)
// Since we can't patch DigRestSiteOption.OnSelect, we track times it offered the dig option
// This is a best-effort proxy; the player may not always choose to dig
[HarmonyPatch(typeof(Shovel), nameof(Shovel.TryModifyRestSiteOptions))]
public sealed class ShovelStats : SimpleCounterStats<Shovel>
{
    public override string Format => "Offered dig {0} times.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(Shovel __instance, bool __result)
    {
        if (!__result) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // TryModifyRestSiteOptions runs when the rest site's options are generated on entry
        // (RestSiteOption.Generate -> Hook.ModifyRestSiteOptions), once per rest site. A combat room
        // generates no rest-site options, so the count stays put.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("enter rest site", () => TestHelpers.EnterRestSite());
        runner.WaitFor(GameEvent.RoomEntered);
        runner.Assert("tracked one dig offer", () =>
            new TestResult(Amount == 1, $"expected Amount == 1, got {Amount}"));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("combat room offers no dig", () =>
            new TestResult(Amount == 1, $"expected Amount still 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// SlingOfCourage: gains strength when entering elite rooms
[HarmonyPatch(typeof(SlingOfCourage), nameof(SlingOfCourage.AfterRoomEntered))]
public sealed class SlingOfCourageStats : SimpleCounterStats<SlingOfCourage>
{
    public override string Format => "Gained {0} [gold]Strength[/gold].";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(SlingOfCourage __instance, AbstractRoom room)
    {
        if (room.RoomType != RoomType.Elite) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Strength.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        // Room guard: a non-elite room (rest site) grants nothing.
        runner.Do("enter rest site", () => TestHelpers.EnterRestSite());
        runner.WaitFor(GameEvent.RoomEntered);
        runner.Assert("non-elite room grants nothing", () =>
            new TestResult(Amount == 0, $"expected Amount == 0, got {Amount}"));
        runner.Do("start elite fight", () => TestHelpers.StartEliteFight());
        runner.WaitFor(GameEvent.RoomEntered);
        runner.Assert("tracked strength", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Strength.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// Akabeko: gains vigor at start of first turn each combat
[HarmonyPatch(typeof(Akabeko), nameof(Akabeko.AfterSideTurnStart))]
public sealed class AkabekoStats : SimpleCounterStats<Akabeko>
{
    public override string Format => "Gained {0} [gold]Vigor[/gold].";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(Akabeko __instance, CombatSide side, ICombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (__instance.Owner.PlayerCombatState!.TurnNumber > 1) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars["VigorPower"].IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.SideTurnStart);
        runner.Assert("tracked stat", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars["VigorPower"].IntValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected Amount == {expected}, got {Amount}");
        });
        // Turn-1 guard: turn 2's side-turn start (after its PlayerTurnStart) must not grant again.
        runner.Do("end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.WaitFor(GameEvent.SideTurnStart);
        runner.Assert("turn 2 does not grant again", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars["VigorPower"].IntValue;
            return new TestResult(Amount == expected, $"expected Amount still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// MiniRegent: gains strength first time stars are spent each turn
[HarmonyPatch(typeof(MiniRegent), nameof(MiniRegent.AfterStarsSpent))]
public sealed class MiniRegentStats : SimpleCounterStats<MiniRegent>
{
    public override string Format => "Gained {0} [gold]Strength[/gold].";
    private static readonly FieldInfo _usedThisTurnField =
        AccessTools.Field(typeof(MiniRegent), "_usedThisTurn");
    private static bool _wasUnusedBeforeCall;

    public static void Prefix(MiniRegent __instance, Player spender)
    {
        _wasUnusedBeforeCall = spender == __instance.Owner
            && !(bool)_usedThisTurnField.GetValue(__instance)!;
    }

    public static void Postfix(MiniRegent __instance, Player spender)
    {
        if (spender != __instance.Owner) return;
        if (!_wasUnusedBeforeCall) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Strength.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // Cloak of Stars costs 0 energy and 1 star (CanonicalStarCost = 1). Star affordability is a
        // plain PlayerCombatState.Stars check and payment goes CardModel.SpendResources -> SpendStars ->
        // Hook.AfterStarsSpent, for any character. The relic only fires on the first star spend of a
        // turn (_usedThisTurn), so a second Cloak the same turn adds nothing.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("spend stars", () => {
            TestHelpers.AddStars(5);
            TestHelpers.SpawnCard("CLOAK_OF_STARS");
            TestHelpers.PlayCard(0);
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("tracked strength from the first star spend", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Strength.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Do("spend stars again this turn", () => {
            TestHelpers.SpawnCard("CLOAK_OF_STARS");
            TestHelpers.PlayCard(0);
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("second spend this turn does not add", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Strength.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// RoyalPoison: deals self-damage at start of first turn
[HarmonyPatch(typeof(RoyalPoison), nameof(RoyalPoison.AfterPlayerTurnStart))]
public sealed class RoyalPoisonStats : SimpleCounterStats<RoyalPoison>
{
    public override string Format => "Dealt {0} [gold]Damage[/gold] to self.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(RoyalPoison __instance, Player player)
    {
        if (player != __instance.Owner) return;
        if (player.PlayerCombatState!.TurnNumber > 1) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Damage.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked stat", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Damage.IntValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected Amount == {expected}, got {Amount}");
        });
        // Turn-1 guard: AfterPlayerTurnStart on turn 2 must not deal (or count) again.
        runner.Do("end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("turn 2 does not deal again", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Damage.IntValue;
            return new TestResult(Amount == expected, $"expected Amount still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// Shuriken: gains strength every 3 attacks played per turn
[HarmonyPatch(typeof(Shuriken), nameof(Shuriken.AfterCardPlayed))]
public sealed class ShurikenStats : SimpleCounterStats<Shuriken>
{
    public override string Format => "Gained {0} [gold]Strength[/gold].";
    private static readonly FieldInfo _attacksField =
        AccessTools.Field(typeof(Shuriken), "_attacksPlayedThisTurn");

    public static void Postfix(Shuriken __instance, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != __instance.Owner) return;
        if (cardPlay.Card.Type != CardType.Attack) return;
        if (!CombatManager.Instance.IsInProgress) return;
        int threshold = __instance.DynamicVars.Cards.IntValue;
        int attacks = (int)_attacksField.GetValue(__instance)!;
        if (attacks % threshold != 0) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Strength.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        // Threshold guard: the Strength lands on the 3rd Attack of the turn, not before. Cards are
        // played highest index first so the remaining Shivs keep their indices.
        runner.Do("add energy + god mode + protect enemy", () => { TestHelpers.AddEnergy(10); TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); });
        runner.Do("spawn 3 shivs, play the first", () => {
            TestHelpers.SpawnCard("SHIV");
            TestHelpers.SpawnCard("SHIV");
            TestHelpers.SpawnCard("SHIV");
            TestHelpers.PlayCard(2, 0);
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Do("play the second attack", () => TestHelpers.PlayCard(1, 0));
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("two attacks do not trigger", () =>
            new TestResult(Amount == 0, $"expected Amount == 0, got {Amount}"));
        runner.Do("play the third attack", () => TestHelpers.PlayCard(0, 0));
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("tracked stat", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Strength.IntValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected Amount == {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// Kunai: gains dexterity every 3 attacks played per turn
[HarmonyPatch(typeof(Kunai), nameof(Kunai.AfterCardPlayed))]
public sealed class KunaiStats : SimpleCounterStats<Kunai>
{
    public override string Format => "Gained {0} [gold]Dexterity[/gold].";
    private static readonly FieldInfo _attacksField =
        AccessTools.Field(typeof(Kunai), "_attacksPlayedThisTurn");

    public static void Postfix(Kunai __instance, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != __instance.Owner) return;
        if (cardPlay.Card.Type != CardType.Attack) return;
        if (!CombatManager.Instance.IsInProgress) return;
        int threshold = __instance.DynamicVars.Cards.IntValue;
        int attacks = (int)_attacksField.GetValue(__instance)!;
        if (attacks % threshold != 0) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Dexterity.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        // Threshold guard: the Dexterity lands on the 3rd Attack of the turn, not before. Cards are
        // played highest index first so the remaining Shivs keep their indices.
        runner.Do("add energy + god mode + protect enemy", () => { TestHelpers.AddEnergy(10); TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); });
        runner.Do("spawn 3 shivs, play the first", () => {
            TestHelpers.SpawnCard("SHIV");
            TestHelpers.SpawnCard("SHIV");
            TestHelpers.SpawnCard("SHIV");
            TestHelpers.PlayCard(2, 0);
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Do("play the second attack", () => TestHelpers.PlayCard(1, 0));
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("two attacks do not trigger", () =>
            new TestResult(Amount == 0, $"expected Amount == 0, got {Amount}"));
        runner.Do("play the third attack", () => TestHelpers.PlayCard(0, 0));
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("tracked stat", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Dexterity.IntValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected Amount == {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// Nunchaku: gains energy every 10 attacks played
[HarmonyPatch(typeof(Nunchaku), nameof(Nunchaku.AfterCardPlayed))]
public sealed class NunchakuStats : SimpleCounterStats<Nunchaku>
{
    public override string Format => "Gained {0} [gold]Energy[/gold].";
    public static void Postfix(Nunchaku __instance, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != __instance.Owner) return;
        if (cardPlay.Card.Type != CardType.Attack) return;
        if (!CombatManager.Instance.IsInProgress) return;
        int threshold = __instance.DynamicVars.Cards.IntValue;
        if (__instance.AttacksPlayed % threshold != 0) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Energy.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        // Threshold guard: Nunchaku's AttacksPlayed is a saved counter with no per-turn reset, so nine
        // attacks on turn 1 give nothing and the tenth on turn 2 gives the Energy.
        runner.Do("add energy + god mode + protect enemy", () => { TestHelpers.AddEnergy(20); TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); });
        runner.Do("play 9 attacks + end turn", () => {
            for (int i = 0; i < 9; i++) TestHelpers.SpawnCard("SHIV");
            TestHelpers.PlayThenEndTurn(9, 0);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 30000);
        runner.Assert("nine attacks do not trigger", () =>
            new TestResult(Amount == 0, $"expected Amount == 0, got {Amount}"));
        runner.Do("play the tenth attack", () => {
            TestHelpers.SpawnCard("SHIV");
            TestHelpers.PlayCard(0, 0);
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("tracked stat", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Energy.IntValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected Amount == {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// GremlinHorn: gains energy and draws card on enemy death
[HarmonyPatch(typeof(GremlinHorn), nameof(GremlinHorn.AfterDeath))]
public sealed class GremlinHornStats : SimpleCounterStats<GremlinHorn>
{
    public override string Format => "Triggered {0} times (drew cards + gained [gold]Energy[/gold]).";
    public static void Postfix(GremlinHorn __instance, Creature target)
    {
        if (target.Side == __instance.Owner.Creature.Side) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // AfterDeath counts deaths on the other side; NIBBITS_WEAK is a single Nibbit, so one kill is
        // exactly one trigger.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("kill enemy", () => TestHelpers.DealDamage(9999));
        runner.WaitFor(GameEvent.Death);
        runner.Assert("tracked one enemy death", () =>
            new TestResult(Amount == 1, $"expected Amount == 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// Vajra: gains strength at combat start
[HarmonyPatch(typeof(Vajra), nameof(Vajra.AfterRoomEntered))]
public sealed class VajraStats : SimpleCounterStats<Vajra>
{
    public override string Format => "Gained {0} [gold]Strength[/gold].";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(Vajra __instance, AbstractRoom room)
    {
        if (room is not CombatRoom) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Strength.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        // Room guard: AfterRoomEntered for a non-combat room grants nothing.
        runner.Do("enter rest site", () => TestHelpers.EnterRestSite());
        runner.WaitFor(GameEvent.RoomEntered);
        runner.Assert("non-combat room grants nothing", () =>
            new TestResult(Amount == 0, $"expected Amount == 0, got {Amount}"));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.CombatStart);
        runner.Assert("tracked stat", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Strength.IntValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected Amount == {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// PetrifiedToad: generates a PotionShapedRock before each combat
[HarmonyPatch(typeof(PetrifiedToad), nameof(PetrifiedToad.BeforeCombatStartLate))]
public sealed class PetrifiedToadStats : SimpleCounterStats<PetrifiedToad>
{
    public override string Format => "Generated {0} potions.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(PetrifiedToad __instance) =>
        Track(__instance, s => s.Amount++);

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.CombatStart);
        runner.Assert("tracked stat", () =>
            new TestResult(Amount == 1, $"expected Amount == 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// Toolbox: offers colorless card choice at start of combat
[HarmonyPatch(typeof(Toolbox), nameof(Toolbox.BeforeHandDraw))]
public sealed class ToolboxStats : SimpleCounterStats<Toolbox>
{
    public override string Format => "Offered cards {0} times.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(Toolbox __instance, Player player, ICombatState combatState)
    {
        if (player != __instance.Owner) return;
        if (__instance.Owner.PlayerCombatState!.TurnNumber != 1) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // BeforeHandDraw on turn 1 tracks, then awaits CardSelectCmd.FromChooseACardScreen; the auto
        // selector (pushed before the fight so it is in place when the prompt opens) answers it and the
        // turn proceeds. Turn 2's hand draw is excluded by the TurnNumber == 1 guard.
        runner.Do("auto-answer the card choice + add relic", () => {
            TestHelpers.PushAutoCardSelector();
            TestHelpers.AddRelic(RelicId);
        });
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked one card offer", () =>
            new TestResult(Amount == 1, $"expected Amount == 1, got {Amount}"));
        runner.Do("end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("turn 2 offers nothing", () =>
            new TestResult(Amount == 1, $"expected Amount still 1, got {Amount}"));
        runner.Cleanup(() => {
            TestHelpers.PopCardSelector();
            TestHelpers.CloseOverlays();
            TestHelpers.EnableGodMode();
            TestHelpers.RemoveRelic(RelicId);
            Reset();
        });
    }
#endif
}

// DarkstonePeriapt: gains max HP when curses enter deck
[HarmonyPatch(typeof(DarkstonePeriapt), nameof(DarkstonePeriapt.AfterCardChangedPiles))]
public sealed class DarkstonePeriaptStats : SimpleCounterStats<DarkstonePeriapt>
{
    public override string Format => "Gained {0} max HP.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(DarkstonePeriapt __instance, CardModel card)
    {
        CardPile? pile = card.Pile;
        if (pile == null || pile.Type != PileType.Deck) return;
        if (card.Owner != __instance.Owner) return;
        if (card.Type != CardType.Curse) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.MaxHp.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // AfterCardChangedPiles reaches the relic after the deck-add tween, so wait for
        // CardChangedPiles. The relic's CreatureCmd.GainMaxHp -> SetMaxHp raises MaxHp before its first
        // await, so the real max HP has moved by the time the hook's postfix signals the event.
        int maxHpBefore = 0;
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("add a curse to deck", () => {
            maxHpBefore = TestHelpers.Player!.Creature.MaxHp;
            TestHelpers.AddCardToDeck("CLUMSY");
        });
        runner.WaitFor(GameEvent.CardChangedPiles);
        runner.Assert("tracked max HP from the curse", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.MaxHp.IntValue ?? -1;
            var gained = TestHelpers.Player!.Creature.MaxHp - maxHpBefore;
            return new TestResult(expected > 0 && Amount == expected && gained == expected,
                $"expected {expected} (max HP +{expected}), got {Amount} (max HP +{gained})");
        });
        runner.Do("add a non-curse to deck", () => TestHelpers.AddCardToDeck("STRIKE_IRONCLAD"));
        runner.WaitFor(GameEvent.CardChangedPiles);
        runner.Assert("non-curse does not add", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.MaxHp.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// Girya: gains strength at combat start based on times lifted
[HarmonyPatch(typeof(Girya), nameof(Girya.AfterRoomEntered))]
public sealed class GiryaStats : SimpleCounterStats<Girya>
{
    public override string Format => "Gained {0} [gold]Strength[/gold].";
    public override StatCadence Cadence => StatCadence.Combat;
    public static void Postfix(Girya __instance, AbstractRoom room)
    {
        if (__instance.TimesLifted <= 0) return;
        if (room is not CombatRoom) return;
        Track(__instance, s => s.Amount += __instance.TimesLifted);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // Girya.TryModifyRestSiteOptions adds a LiftRestSiteOption (OptionId "LIFT") while
        // TimesLifted < 3; its OnSelect does TimesLifted++ synchronously. The Strength (and this stat)
        // then lands in AfterRoomEntered for the next combat room; the rest site itself counts nothing.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("enter rest site", () => TestHelpers.EnterRestSite());
        runner.WaitFor(GameEvent.RoomEntered);
        runner.Do("lift", () => TestHelpers.SelectRestSiteOption("LIFT"));
        runner.Assert("lifted once, nothing tracked outside combat", () => {
            var lifted = TestHelpers.GetRelic<Girya>()?.TimesLifted ?? -1;
            return new TestResult(lifted == 1 && Amount == 0, $"expected TimesLifted=1 Amount=0, got TimesLifted={lifted} Amount={Amount}");
        });
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.CombatStart);
        runner.Assert("tracked strength from lifts", () => {
            var lifted = TestHelpers.GetRelic<Girya>()?.TimesLifted ?? -1;
            return new TestResult(lifted == 1 && Amount == lifted, $"expected Amount == TimesLifted == 1, got Amount={Amount} TimesLifted={lifted}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// Brimstone: gains strength each turn (also gives enemies strength)
[HarmonyPatch(typeof(Brimstone), nameof(Brimstone.AfterSideTurnStart))]
public sealed class BrimstoneStats : SimpleCounterStats<Brimstone>
{
    public override string Format => "Gained {0} [gold]Strength[/gold].";
    public static void Postfix(Brimstone __instance, CombatSide side)
    {
        if (side != __instance.Owner.Creature.Side) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars["SelfStrength"].IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.SideTurnStart);
        runner.Assert("tracked stat", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars["SelfStrength"].IntValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected Amount == {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// SneckoSkull: adds extra poison to all poison applications
[HarmonyPatch(typeof(SneckoSkull), nameof(SneckoSkull.AfterModifyingPowerAmountGiven))]
public sealed class SneckoSkullStats : SimpleCounterStats<SneckoSkull>
{
    public override string Format => "Added {0} extra [gold]Poison[/gold].";
    public static void Postfix(SneckoSkull __instance) =>
        Track(__instance, s => s.Amount += __instance.DynamicVars.Poison.IntValue);

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // ModifyPowerAmountGivenAdditive adds the bonus when the giver is the owner, and only then is the
        // relic in the givenModifiers list that AfterModifyingPowerAmountGiven iterates. PowerCmd.Apply
        // skips the "given" pass entirely for a null applier, which is what the power console command
        // passes, so that poison never counts. It goes first so the card play (which takes far longer)
        // is the sync point for both. No god mode: its powers also run through this pipeline.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("apply poison with no applier, then play a poison card", () => {
            TestHelpers.ProtectEnemy();
            TestHelpers.ApplyPower("POISON_POWER", 3, 1);
            TestHelpers.AddEnergy(10);
            TestHelpers.SpawnCard("DEADLY_POISON");
            TestHelpers.PlayCard(0, 0);
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("tracked extra poison for the owner's application only", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Poison.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// TwistedFunnel: applies poison to all enemies at combat start
[HarmonyPatch(typeof(TwistedFunnel), nameof(TwistedFunnel.BeforeSideTurnStart))]
public sealed class TwistedFunnelStats : SimpleCounterStats<TwistedFunnel>
{
    public override string Format => "Applied {0} [gold]Poison[/gold].";
    public override StatCadence Cadence => StatCadence.Combat;
    public static void Postfix(TwistedFunnel __instance, CombatSide side, ICombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (__instance.Owner.PlayerCombatState!.TurnNumber > 1) return;
        int enemies = __instance.Owner.Creature.CombatState!.HittableEnemies.Count;
        Track(__instance, s => s.Amount += __instance.DynamicVars["PoisonPower"].IntValue * enemies);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        int snapshot = 0;
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("snapshot", () => snapshot = Amount);
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.CombatStart);
        runner.WaitFor(GameEvent.SideTurnStart);
        runner.Assert("tracked poison", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var enemyCount = TestHelpers.Player!.Creature.CombatState!.HittableEnemies.Count;
            var expected = relic!.DynamicVars["PoisonPower"].IntValue * enemyCount;
            var delta = Amount - snapshot;
            return new TestResult(expected > 0 && delta == expected, $"expected delta {expected}, got {delta}");
        });
        // Turn-1 guard: turn 2's side-turn start (after its PlayerTurnStart) must not apply again.
        runner.Do("end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.WaitFor(GameEvent.SideTurnStart);
        runner.Assert("turn 2 does not apply again", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var enemyCount = TestHelpers.Player!.Creature.CombatState!.HittableEnemies.Count;
            var expected = relic!.DynamicVars["PoisonPower"].IntValue * enemyCount;
            var delta = Amount - snapshot;
            return new TestResult(delta == expected, $"expected delta still {expected}, got {delta}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// Pendulum: draws cards every N turns at turn start
public sealed class PendulumStats : SimpleCounterStats<Pendulum>
{
    public override string Format => "Drew {0} cards.";

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        // BeforeHandDraw advances TurnsSeen = (TurnsSeen + 1) % Turns each turn and ModifyHandDraw adds
        // Cards on the turn it wraps to 0: with Turns = 3 that is turn 3 (1, 2, 0). Both run inside
        // turn setup, so they have already run for turn K+1 when that turn's PlayerTurnStart fires.
        runner.Assert("no draw on turn 1", () =>
            new TestResult(Amount == 0, $"expected Amount == 0, got {Amount}"));
        runner.Do("enable god mode + protect enemy", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); });
        runner.Do("end turn 1", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("no draw on turn 2", () =>
            new TestResult(Amount == 0, $"expected Amount == 0, got {Amount}"));
        runner.Do("end turn 2", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked cards drawn on turn 3", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Cards.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// 0.110+: ModifyHandDraw adds the cards when the turn counter wraps, so read the bonus off the hook.
[HarmonyPatch]
internal static class PendulumModifyHandDrawPatch
{
    public static IEnumerable<MethodBase> TargetMethods() =>
        PatchTarget.DeclaredOrNone(typeof(Pendulum), nameof(Pendulum.ModifyHandDraw));

    public static void Postfix(Pendulum __instance, decimal __result, decimal __1)
    {
        if (__result <= __1) return;
        PendulumStats.Track(__instance, s => s.Amount += (int)(__result - __1));
    }
}

// 0.107.1: the draw happened inside AfterPlayerTurnStart, which also advances the counter, so the
// activation has to be predicted before the hook runs.
[HarmonyPatch]
internal static class PendulumAfterPlayerTurnStartPatch
{
    private static readonly FieldInfo? TurnsSeenField = AccessTools.Field(typeof(Pendulum), "_turnsSeen");
    [ThreadStatic] private static bool _willDraw;

    // Never bind alongside the ModifyHandDraw patch, or the draw would be counted twice.
    public static IEnumerable<MethodBase> TargetMethods() =>
        AccessTools.DeclaredMethod(typeof(Pendulum), nameof(Pendulum.ModifyHandDraw)) != null
            ? Array.Empty<MethodBase>()
            : PatchTarget.DeclaredOrNone(typeof(Pendulum), nameof(Pendulum.AfterPlayerTurnStart));

    public static void Prefix(Pendulum __instance, Player player)
    {
        _willDraw = false;
        if (player != __instance.Owner || TurnsSeenField == null) return;
        int turnsSeen = (int)TurnsSeenField.GetValue(__instance)!;
        int turns = __instance.DynamicVars["Turns"].IntValue;
        if (turns <= 0) return;
        // The relic increments TurnsSeen then draws when it wraps to 0.
        _willDraw = (turnsSeen + 1) % turns == 0;
    }

    public static void Postfix(Pendulum __instance)
    {
        if (!_willDraw) return;
        PendulumStats.Track(__instance, s => s.Amount += __instance.DynamicVars.Cards.IntValue);
    }
}

// ChosenCheese: gains max HP at end of combat
[HarmonyPatch(typeof(ChosenCheese), nameof(ChosenCheese.AfterCombatEnd))]
public sealed class ChosenCheeseStats : SimpleCounterStats<ChosenCheese>
{
    public override string Format => "Gained {0} max HP.";
    public override StatCadence Cadence => StatCadence.Total;
    protected override string FormatStat(int amount) => FormatStatGreen(amount);
    public static void Postfix(ChosenCheese __instance) =>
        Track(__instance, s => s.Amount += __instance.DynamicVars["MaxHp"].IntValue);

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // AfterCombatEnd fires when combat ends.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("win combat", () => TestHelpers.WinCombat());
        runner.WaitFor(GameEvent.CombatEnd);
        runner.Assert("tracked max HP gain", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars["MaxHp"].IntValue;
            return new TestResult(Amount == expected, $"expected Amount == {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// BookOfFiveRings: heals when adding cards to deck
[HarmonyPatch(typeof(BookOfFiveRings), nameof(BookOfFiveRings.AfterCardChangedPiles))]
public sealed class BookOfFiveRingsStats : SimpleCounterStats<BookOfFiveRings>
{
    public override string Format => "Healed {0} HP from adding cards.";
    public override StatCadence Cadence => StatCadence.Total;
    protected override string FormatStat(int amount) => FormatStatGreen(amount);

    // CardsAdded is a [SavedProperty], not a DynamicVar. The relic increments it and, on the wrap to 0,
    // awaits CreatureCmd.Heal, whose HealInternal runs before its first await, so by the time this Postfix
    // runs (the method's first yield) the HP is already applied. Counting the HP delta caps the heal at
    // the missing HP, like the other heal trackers.
    public static void Prefix(BookOfFiveRings __instance, out (int cardsAdded, int hp) __state) =>
        __state = (__instance.CardsAdded, __instance.Owner.Creature.CurrentHp);

    public static void Postfix(BookOfFiveRings __instance, (int cardsAdded, int hp) __state)
    {
        int cards = __instance.CardsAdded;
        if (cards <= __state.cardsAdded || cards % __instance.DynamicVars.Cards.IntValue != 0) return;
        int heal = __instance.Owner.Creature.CurrentHp - __state.hp;
        if (heal <= 0) return;
        Track(__instance, s => s.Amount += heal);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // AfterCardChangedPiles counts each card entering the permanent deck and heals Heal (20) when
        // CardsAdded wraps past Cards (5). Each deck add reaches the hook only after its tween, hence one
        // CardChangedPiles wait per card. HP is set 10 below max before the fifth add, so the tracked heal
        // is capped at the 10 HP actually restored.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        for (int i = 1; i <= 4; i++)
        {
            runner.Do($"add card {i} to deck", () => TestHelpers.AddCardToDeck("STRIKE_IRONCLAD"));
            runner.WaitFor(GameEvent.CardChangedPiles);
        }
        runner.Assert("four cards do not heal", () =>
            new TestResult(Amount == 0, $"expected Amount == 0, got {Amount}"));
        runner.Do("set HP 10 below max", () =>
            TestHelpers.SetPlayerHp(TestHelpers.Player!.Creature.MaxHp - 10));
        runner.Do("add card 5 to deck", () => TestHelpers.AddCardToDeck("STRIKE_IRONCLAD"));
        runner.WaitFor(GameEvent.CardChangedPiles);
        runner.Assert("tracked only the HP actually healed on the fifth card", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var heal = relic?.DynamicVars.Heal.IntValue ?? -1;
            var expected = Math.Min(heal, 10);
            return new TestResult(heal > 10 && Amount == expected, $"expected {expected} (Heal {heal} capped at 10 missing HP), got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// BagOfMarbles: applies Vulnerable to all enemies at combat start
[HarmonyPatch(typeof(BagOfMarbles), nameof(BagOfMarbles.BeforeSideTurnStart))]
public sealed class BagOfMarblesStats : SimpleCounterStats<BagOfMarbles>
{
    public override string Format => "Applied [gold]Vulnerable[/gold] {0} times.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(BagOfMarbles __instance, CombatSide side, ICombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (__instance.Owner.PlayerCombatState!.TurnNumber > 1) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // BeforeSideTurnStart runs inside turn setup, so it has fired by the turn's PlayerTurnStart.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked turn-1 vulnerable", () =>
            new TestResult(Amount == 1, $"expected Amount == 1, got {Amount}"));
        // Turn 2 is past the turn-1 guard: nothing more is counted.
        runner.Do("god mode + protect enemy + end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("still 1 on turn 2", () =>
            new TestResult(Amount == 1, $"expected Amount still 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// Bellows: upgrades hand on turn 1
[HarmonyPatch(typeof(Bellows), nameof(Bellows.AfterPlayerTurnStart))]
public sealed class BellowsStats : SimpleCounterStats<Bellows>
{
    public override string Format => "Upgraded {0} hands.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(Bellows __instance, Player player)
    {
        if (player != __instance.Owner) return;
        if (player.PlayerCombatState!.TurnNumber > 1) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked stat", () =>
            new TestResult(Amount == 1, $"expected Amount == 1, got {Amount}"));
        // Turn 2 is past the turn-1 guard: the turn-2 AfterPlayerTurnStart has run by this signal.
        runner.Do("god mode + protect enemy + end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("still 1 on turn 2", () =>
            new TestResult(Amount == 1, $"expected Amount still 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// BronzeScales: applies Thorns at combat start
[HarmonyPatch(typeof(BronzeScales), nameof(BronzeScales.AfterRoomEntered))]
public sealed class BronzeScalesStats : SimpleCounterStats<BronzeScales>
{
    public override string Format => "Applied {0} [gold]Thorns[/gold].";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(BronzeScales __instance, AbstractRoom room)
    {
        if (room is not CombatRoom) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars["ThornsPower"].IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.CombatStart);
        runner.Assert("tracked stat", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars["ThornsPower"].IntValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected Amount == {expected}, got {Amount}");
        });
        // A non-combat room does not count.
        runner.Do("enter rest site", () => TestHelpers.EnterRestSite());
        runner.WaitFor(GameEvent.RoomEntered, 15000);
        runner.Assert("still Thorns after a rest site", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars["ThornsPower"].IntValue;
            return new TestResult(Amount == expected, $"expected Amount still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// Crossbow: generates a free attack each turn
[HarmonyPatch(typeof(Crossbow), nameof(Crossbow.AfterSideTurnStart))]
public sealed class CrossbowStats : SimpleCounterStats<Crossbow>
{
    public override string Format => "Generated {0} free attacks.";
    public static void Postfix(Crossbow __instance, CombatSide side)
    {
        if (side != __instance.Owner.Creature.Side) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.SideTurnStart);
        runner.Assert("tracked stat", () =>
            new TestResult(Amount == 1, $"expected Amount == 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// DataDisk: applies Focus at combat start
[HarmonyPatch(typeof(DataDisk), nameof(DataDisk.AfterRoomEntered))]
public sealed class DataDiskStats : SimpleCounterStats<DataDisk>
{
    public override string Format => "Applied {0} [gold]Focus[/gold].";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(DataDisk __instance, AbstractRoom room)
    {
        if (room is not CombatRoom) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars["FocusPower"].IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.CombatStart);
        runner.Assert("tracked stat", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars["FocusPower"].IntValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected Amount == {expected}, got {Amount}");
        });
        // A non-combat room does not count.
        runner.Do("enter rest site", () => TestHelpers.EnterRestSite());
        runner.WaitFor(GameEvent.RoomEntered, 15000);
        runner.Assert("still Focus after a rest site", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars["FocusPower"].IntValue;
            return new TestResult(Amount == expected, $"expected Amount still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// EmberTea: applies Strength at combat start (limited uses)
[HarmonyPatch(typeof(EmberTea), nameof(EmberTea.AfterRoomEntered))]
public sealed class EmberTeaStats : SimpleCounterStats<EmberTea>
{
    public override string Format => "Applied {0} [gold]Strength[/gold].";
    public override StatCadence Cadence => StatCadence.Total;
    private static bool _willApply;

    public static void Prefix(EmberTea __instance, AbstractRoom room)
    {
        _willApply = !__instance.IsUsedUp && room is CombatRoom;
    }

    public static void Postfix(EmberTea __instance)
    {
        if (!_willApply) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Strength.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.CombatStart);
        runner.Assert("tracked stat", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Strength.IntValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected Amount == {expected}, got {Amount}");
        });
        // A non-combat room does not count.
        runner.Do("enter rest site", () => TestHelpers.EnterRestSite());
        runner.WaitFor(GameEvent.RoomEntered, 15000);
        runner.Assert("still Strength after a rest site", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Strength.IntValue;
            return new TestResult(Amount == expected, $"expected Amount still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// FakeSneckoEye: applies Confused at combat start
[HarmonyPatch(typeof(FakeSneckoEye), nameof(FakeSneckoEye.BeforeCombatStart))]
public sealed class FakeSneckoEyeStats : SimpleCounterStats<FakeSneckoEye>
{
    public override string Format => "Applied [gold]Confused[/gold] {0} times.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(FakeSneckoEye __instance) =>
        Track(__instance, s => s.Amount++);

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.CombatStart);
        runner.Assert("tracked stat", () =>
            new TestResult(Amount == 1, $"expected Amount == 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// FencingManual: gains Forge on turn 1
[HarmonyPatch(typeof(FencingManual), nameof(FencingManual.AfterSideTurnStart))]
public sealed class FencingManualStats : SimpleCounterStats<FencingManual>
{
    public override string Format => "Gained {0} [gold]Forge[/gold].";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(FencingManual __instance, CombatSide side, ICombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (__instance.Owner.PlayerCombatState!.TurnNumber > 1) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Forge.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.SideTurnStart);
        runner.Assert("tracked stat", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Forge.IntValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected Amount == {expected}, got {Amount}");
        });
        // Turn 2 is past the turn-1 guard. The player's turn-2 AfterSideTurnStart fires after
        // PlayerTurnStart, so wait for that SideTurnStart before asserting.
        runner.Do("god mode + protect enemy + end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.WaitFor(GameEvent.SideTurnStart, 15000);
        runner.Assert("still Forge on turn 2", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Forge.IntValue;
            return new TestResult(Amount == expected, $"expected Amount still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// FuneraryMask: generates Soul cards at hand draw on turn 1
[HarmonyPatch(typeof(FuneraryMask), nameof(FuneraryMask.BeforeHandDraw))]
public sealed class FuneraryMaskStats : SimpleCounterStats<FuneraryMask>
{
    public override string Format => "Generated {0} Soul cards.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(FuneraryMask __instance, Player player, ICombatState combatState)
    {
        if (player != __instance.Owner) return;
        if (__instance.Owner.PlayerCombatState!.TurnNumber != 1) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Cards.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        // BeforeHandDraw fires during turn-1 hand draw, after SideTurnStart — wait for PlayerTurnStart.
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked stat", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Cards.IntValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected Amount == {expected}, got {Amount}");
        });
        // Turn 2 is past the turn-1 guard: its hand draw has run by the turn-2 PlayerTurnStart.
        runner.Do("god mode + protect enemy + end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("still Cards on turn 2", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Cards.IntValue;
            return new TestResult(Amount == expected, $"expected Amount still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// GamePiece: draws cards when Powers are played
[HarmonyPatch(typeof(GamePiece), nameof(GamePiece.AfterCardPlayed))]
public sealed class GamePieceStats : SimpleCounterStats<GamePiece>
{
    public override string Format => "Drew {0} cards from Powers.";

    // The draw is awaited (CardPileCmd.Draw), so the Postfix wraps the returned Task and counts the
    // hand-size increase once the draw has completed: an empty draw and discard pile draws nothing.
    // __state is the hand count before the relic ran, or -1 when the relic does not act.
    public static void Prefix(GamePiece __instance, CardPlay cardPlay, out int __state)
    {
        __state = -1;
        if (cardPlay.Card.Owner != __instance.Owner) return;
        if (cardPlay.Card.Type != CardType.Power) return;
        if (!CombatManager.Instance.IsInProgress) return;
        __state = PileType.Hand.GetPile(__instance.Owner).Cards.Count;
    }

    public static void Postfix(GamePiece __instance, ref Task __result, int __state)
    {
        if (__state < 0) return;
        __result = CountWhenDone(__instance, __result, __state);
    }

    private static async Task CountWhenDone(GamePiece relic, Task inner, int handBefore)
    {
        await inner;
        int drawn = PileType.Hand.GetPile(relic.Owner).Cards.Count - handBefore;
        if (drawn <= 0) return;
        Track(relic, s => s.Amount += drawn);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // Negative first: the deck is cleared, so the draw and discard piles are empty and the relic's
        // draw after a Power finds nothing. Then two Strikes go to the draw pile and a second Power draws Cards.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("play a power with an empty draw pile", () =>
        {
            TestHelpers.AddEnergy(3);
            TestHelpers.SpawnCard("DEMON_FORM");
            TestHelpers.PlayCard(TestHelpers.FindCardInHand(CardType.Power));
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("nothing to draw, nothing counted", () =>
            new TestResult(Amount == 0, $"expected Amount == 0, got {Amount}"));
        runner.Do("seed the draw pile + play a power", () =>
        {
            TestHelpers.SpawnCard("STRIKE_IRONCLAD", "draw");
            TestHelpers.SpawnCard("STRIKE_IRONCLAD", "draw");
            TestHelpers.AddEnergy(3);
            TestHelpers.SpawnCard("DEMON_FORM");
            TestHelpers.PlayCard(TestHelpers.FindCardInHand(CardType.Power));
        });
        runner.WaitUntil("the draw completed", () => Amount > 0);
        runner.Assert("tracked the cards drawn", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Cards.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        // A non-Power does not count, though the draw pile still holds a Strike.
        runner.Do("play an attack", () => { TestHelpers.SpawnCard("STRIKE_IRONCLAD"); TestHelpers.PlayCard(TestHelpers.FindCardInHand(CardType.Attack), 0); });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("still Cards after an attack", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Cards.IntValue ?? -1;
            return new TestResult(Amount == expected, $"expected Amount still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// GoldPlatedCables: doubles first orb's passive trigger
[HarmonyPatch(typeof(GoldPlatedCables), nameof(GoldPlatedCables.ModifyOrbPassiveTriggerCounts))]
public sealed class GoldPlatedCablesStats : SimpleCounterStats<GoldPlatedCables>
{
    public override string Format => "Doubled first orb passive {0} times.";
    public static void Postfix(GoldPlatedCables __instance, OrbModel orb, int __result, int triggerCount)
    {
        if (__result <= triggerCount) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // Ironclad has no orb slots, but OrbCmd.Channel adds one when a character with
        // BaseOrbSlotCount == 0 channels into an empty queue, so Cracked Core's turn-1 Lightning
        // gives the player a first orb. Its passive triggers at turn end (BeforeTurnEndOrbTrigger ->
        // TriggerPassive -> Hook.ModifyOrbPassiveTriggerCount), where the relic adds one trigger.
        runner.Do("add relic + Cracked Core", () => { TestHelpers.AddRelic(RelicId); TestHelpers.AddRelic("CRACKED_CORE"); });
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("nothing before the first turn end", () =>
            new TestResult(Amount == 0, $"expected Amount == 0 before a turn end, got {Amount}"));
        runner.Do("god mode + protect enemy + end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("doubled the first orb's passive once", () =>
            new TestResult(Amount == 1, $"expected Amount == 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic("CRACKED_CORE"); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// HandDrill: applies Vulnerable when block is broken
public sealed class HandDrillStats : SimpleCounterStats<HandDrill>
{
    public override string Format => "Applied [gold]Vulnerable[/gold] {0} times.";

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // Fires on block break. The Nibbit has no block on turn 1, so an unblocked hit first
        // (nothing to break), then give it block and attack to break it.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("hit an unblocked enemy", () => {
            TestHelpers.AddEnergy(10);
            TestHelpers.ProtectEnemy();
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.PlayCard(0, 0);
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("no block broken by an unblocked hit", () =>
            new TestResult(Amount == 0, $"expected Amount == 0, got {Amount}"));
        runner.Do("give enemy block and attack + end turn", () => {
            TestHelpers.EnableGodMode();
            TestHelpers.GiveEnemyBlock(1);
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.PlayThenEndTurn(1, 0);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked block break", () =>
            new TestResult(Amount == 1, $"expected 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// 0.110+: a dedicated hook, firing once per break.
[HarmonyPatch]
internal static class HandDrillAfterBlockBrokenPatch
{
    public static IEnumerable<MethodBase> TargetMethods() =>
        PatchTarget.DeclaredOrNone(typeof(HandDrill), nameof(HandDrill.AfterBlockBroken));

    public static void Postfix(HandDrill __instance, Creature target, Creature? breaker)
    {
        if (breaker != __instance.Owner.Creature && breaker?.PetOwner != __instance.Owner) return;
        if (target.IsPlayer) return;
        HandDrillStats.Track(__instance, s => s.Amount++);
    }
}

// 0.107.1: fired for every damage instance; the relic checked WasBlockBroken itself.
[HarmonyPatch]
internal static class HandDrillAfterDamageGivenPatch
{
    public static IEnumerable<MethodBase> TargetMethods() =>
        PatchTarget.DeclaredOrNone(typeof(HandDrill), nameof(HandDrill.AfterDamageGiven));

    public static void Postfix(HandDrill __instance, Creature? dealer, DamageResult result)
    {
        if (dealer != __instance.Owner.Creature && dealer?.PetOwner != __instance.Owner) return;
        if (!result.WasBlockBroken) return;
        HandDrillStats.Track(__instance, s => s.Amount++);
    }
}

// HelicalDart: gains Dexterity from playing Shivs
[HarmonyPatch(typeof(HelicalDart), nameof(HelicalDart.AfterCardPlayed))]
public sealed class HelicalDartStats : SimpleCounterStats<HelicalDart>
{
    public override string Format => "Gained {0} [gold]Dexterity[/gold] from Shivs.";
    public static void Postfix(HelicalDart __instance, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != __instance.Owner) return;
        if (!cardPlay.Card.Tags.Contains(CardTag.Shiv)) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Dexterity.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("play shiv + end turn", () => {
            TestHelpers.AddEnergy(10);
            TestHelpers.EnableGodMode();
            TestHelpers.ProtectEnemy();
            TestHelpers.SpawnCard("SHIV");
            TestHelpers.PlayThenEndTurn(1, 0);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked stat", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Dexterity.IntValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected Amount == {expected}, got {Amount}");
        });
        // A non-Shiv attack does not count.
        runner.Do("play a strike", () => { TestHelpers.SpawnCard("STRIKE"); TestHelpers.PlayCard(0, 0); });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("still Dexterity after a non-Shiv", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Dexterity.IntValue;
            return new TestResult(Amount == expected, $"expected Amount still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// MusicBox: copies first attack each turn as Ethereal
[HarmonyPatch(typeof(MusicBox), nameof(MusicBox.AfterCardPlayed))]
public sealed class MusicBoxStats : SimpleCounterStats<MusicBox>
{
    public override string Format => "Copied {0} attacks as [gold]Ethereal[/gold].";
    private static readonly FieldInfo _cardBeingPlayedField =
        AccessTools.Field(typeof(MusicBox), "_cardBeingPlayed");
    private static bool _willCopy;

    public static void Prefix(MusicBox __instance, CardPlay cardPlay)
    {
        // The method copies when cardPlay.Card == CardBeingPlayed (set in BeforeCardPlayed)
        _willCopy = cardPlay.Card == (CardModel?)_cardBeingPlayedField.GetValue(__instance);
    }

    public static void Postfix(MusicBox __instance)
    {
        if (!_willCopy) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("play attack", () => {
            TestHelpers.AddEnergy(10);
            TestHelpers.EnableGodMode();
            TestHelpers.ProtectEnemy();
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.PlayCard(0, 0);
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("tracked first attack", () =>
            new TestResult(Amount == 1, $"expected Amount == 1, got {Amount}"));
        // Only the first attack of a turn is copied: a second attack in the same turn is not.
        runner.Do("play a second attack + end turn", () => {
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.PlayThenEndTurn(1, 0);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("still 1 after a second attack", () =>
            new TestResult(Amount == 1, $"expected Amount still 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// OddlySmoothStone: applies Dexterity at combat start
[HarmonyPatch(typeof(OddlySmoothStone), nameof(OddlySmoothStone.AfterRoomEntered))]
public sealed class OddlySmoothStoneStats : SimpleCounterStats<OddlySmoothStone>
{
    public override string Format => "Applied {0} [gold]Dexterity[/gold].";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(OddlySmoothStone __instance, AbstractRoom room)
    {
        if (room is not CombatRoom) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Dexterity.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.CombatStart);
        runner.Assert("tracked stat", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Dexterity.IntValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected Amount == {expected}, got {Amount}");
        });
        // A non-combat room does not count.
        runner.Do("enter rest site", () => TestHelpers.EnterRestSite());
        runner.WaitFor(GameEvent.RoomEntered, 15000);
        runner.Assert("still Dexterity after a rest site", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Dexterity.IntValue;
            return new TestResult(Amount == expected, $"expected Amount still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// ReptileTrinket: gains temporary Strength from potions
[HarmonyPatch(typeof(ReptileTrinket), nameof(ReptileTrinket.AfterPotionUsed))]
public sealed class ReptileTrinketStats : SimpleCounterStats<ReptileTrinket>
{
    public override string Format => "Gained {0} temporary [gold]Strength[/gold] from potions.";
    public static void Postfix(ReptileTrinket __instance, PotionModel potion)
    {
        if (potion.Owner != __instance.Owner) return;
        if (!CombatManager.Instance.IsInProgress) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Strength.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        // Use a potion and wait for the PotionUsed event (the relic tracks in AfterPotionUsed);
        // combining the use + end-turn in one step raced the assert ahead of the potion resolving.
        runner.Do("use potion", () => { TestHelpers.AddPotion("FLEX_POTION"); TestHelpers.UsePotion("FLEX_POTION"); });
        runner.WaitFor(GameEvent.PotionUsed, 15000);
        runner.Assert("tracked strength", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Strength.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// RunicCapacitor: adds orb slots on turn 1
[HarmonyPatch(typeof(RunicCapacitor), nameof(RunicCapacitor.AfterSideTurnStart))]
public sealed class RunicCapacitorStats : SimpleCounterStats<RunicCapacitor>
{
    public override string Format => "Added {0} orb slots.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(RunicCapacitor __instance, CombatSide side, ICombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (__instance.Owner.PlayerCombatState!.TurnNumber > 1) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Repeat.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.SideTurnStart);
        runner.Assert("tracked stat", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Repeat.IntValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected Amount == {expected}, got {Amount}");
        });
        // Turn 2 is past the turn-1 guard. The player's turn-2 AfterSideTurnStart fires after
        // PlayerTurnStart, so wait for that SideTurnStart before asserting.
        runner.Do("god mode + protect enemy + end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.WaitFor(GameEvent.SideTurnStart, 15000);
        runner.Assert("still Repeat on turn 2", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Repeat.IntValue;
            return new TestResult(Amount == expected, $"expected Amount still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// SparklingRouge: gains Strength and Dexterity on block clear in round 3
[HarmonyPatch(typeof(SparklingRouge), nameof(SparklingRouge.AfterBlockCleared))]
public sealed class SparklingRougeStats : SimpleCounterStats<SparklingRouge>
{
    public override string Format => "Gained [gold]Strength[/gold]+[gold]Dexterity[/gold] {0} times.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(SparklingRouge __instance, Creature creature)
    {
        if (creature != __instance.Owner.Creature) return;
        if (__instance.Owner.PlayerCombatState!.TurnNumber != 3) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // Hook.AfterBlockCleared fires for every creature at the start of its turn (block or not),
        // after SwitchSides has advanced TurnNumber and before AfterPlayerTurnStart. So the clear at
        // the start of turn 3 is the one the relic acts on, and it has run by turn 3's PlayerTurnStart.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Do("god mode + protect enemy + end turn 1", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("nothing on turn 2", () =>
            new TestResult(Amount == 0, $"expected Amount == 0 on turn 2, got {Amount}"));
        runner.Do("end turn 2", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked the turn-3 block clear", () =>
            new TestResult(Amount == 1, $"expected Amount == 1 on turn 3, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// StoneCracker: upgrades random upgradable cards in the draw pile at the start of every combat
[HarmonyPatch(typeof(StoneCracker), nameof(StoneCracker.AfterRoomEntered))]
public sealed class StoneCrackerStats : SimpleCounterStats<StoneCracker>
{
    public override string Format => "Upgraded {0} cards.";
    public override StatCadence Cadence => StatCadence.Combat;
    // Prefix: the game upgrades Take(Cards) of the draw pile's upgradable cards before its first await,
    // so a Postfix would find them already upgraded.
    public static void Prefix(StoneCracker __instance, AbstractRoom room)
    {
        if (room is not CombatRoom) return;
        int upgradable = PileType.Draw.GetPile(__instance.Owner).Cards.Count(c => c.IsUpgradable);
        int upgraded = Math.Min(__instance.DynamicVars.Cards.IntValue, upgradable);
        if (upgraded <= 0) return;
        Track(__instance, s => s.Amount += upgraded);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // The draw pile is built at combat start, before any step can spawn into it, so the first fight
        // (empty deck) has nothing to upgrade; two deck Strikes make the second, a Monster fight, upgrade both.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight with an empty deck", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("nothing upgradable, nothing counted", () =>
            new TestResult(Amount == 0, $"expected Amount == 0, got {Amount}"));
        runner.Do("add strike 1 to deck", () => TestHelpers.AddCardToDeck("STRIKE_IRONCLAD"));
        runner.WaitFor(GameEvent.CardChangedPiles);
        runner.Do("add strike 2 to deck", () => TestHelpers.AddCardToDeck("STRIKE_IRONCLAD"));
        runner.WaitFor(GameEvent.CardChangedPiles);
        runner.Do("start monster fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked both strikes upgraded", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = Math.Min(relic?.DynamicVars.Cards.IntValue ?? -1, 2);
            return new TestResult(expected == 2 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// SwordOfJade: applies Strength at combat start
[HarmonyPatch(typeof(SwordOfJade), nameof(SwordOfJade.AfterRoomEntered))]
public sealed class SwordOfJadeStats : SimpleCounterStats<SwordOfJade>
{
    public override string Format => "Applied {0} [gold]Strength[/gold].";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(SwordOfJade __instance, AbstractRoom room)
    {
        if (room is not CombatRoom) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Strength.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.CombatStart);
        runner.Assert("tracked stat", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Strength.IntValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected Amount == {expected}, got {Amount}");
        });
        // A non-combat room does not count.
        runner.Do("enter rest site", () => TestHelpers.EnterRestSite());
        runner.WaitFor(GameEvent.RoomEntered, 15000);
        runner.Assert("still Strength after a rest site", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Strength.IntValue;
            return new TestResult(Amount == expected, $"expected Amount still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// SwordOfStone: tracks elites defeated toward transformation
[HarmonyPatch(typeof(SwordOfStone), nameof(SwordOfStone.AfterCombatVictory))]
public sealed class SwordOfStoneStats : SimpleCounterStats<SwordOfStone>
{
    public override string Format => "Defeated {0} elites.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(SwordOfStone __instance, CombatRoom room)
    {
        if (room.RoomType != RoomType.Elite) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // AfterCombatVictory fires with CombatRoom.RoomType == Elite. Room guard first: a Monster win
        // does not count. Its rewards are awaited and closed before the elite fight starts.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start monster fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("win the monster fight", () => TestHelpers.WinCombat());
        runner.WaitFor(GameEvent.RewardsGenerated, 15000);
        runner.Assert("a monster win does not count", () =>
            new TestResult(Amount == 0, $"expected Amount == 0 after a monster win, got {Amount}"));
        runner.Do("close rewards + start elite fight", () => { TestHelpers.CloseOverlays(); TestHelpers.StartEliteFight(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("nothing before the elite victory", () =>
            new TestResult(Amount == 0, $"expected Amount == 0 before the win, got {Amount}"));
        runner.Do("win the elite fight", () => TestHelpers.WinCombat());
        runner.WaitFor(GameEvent.CombatVictory);
        runner.Assert("tracked elite defeat", () =>
            new TestResult(Amount == 1, $"expected Amount == 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.CloseOverlays(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// SymbioticVirus: channels Dark orbs on turn 1
[HarmonyPatch(typeof(SymbioticVirus), nameof(SymbioticVirus.AfterSideTurnStart))]
public sealed class SymbioticVirusStats : SimpleCounterStats<SymbioticVirus>
{
    public override string Format => "Channeled {0} [gold]Dark[/gold] orbs.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(SymbioticVirus __instance, CombatSide side, ICombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (__instance.Owner.PlayerCombatState!.TurnNumber > 1) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars["Dark"].IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.SideTurnStart);
        runner.Assert("tracked orbs", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars["Dark"].IntValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected Amount == {expected}, got {Amount}");
        });
        // Turn 2 is past the turn-1 guard. The player's turn-2 AfterSideTurnStart fires after
        // PlayerTurnStart, so wait for that SideTurnStart before asserting.
        runner.Do("god mode + protect enemy + end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.WaitFor(GameEvent.SideTurnStart, 15000);
        runner.Assert("still Dark on turn 2", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars["Dark"].IntValue;
            return new TestResult(Amount == expected, $"expected Amount still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// ToastyMittens: gains Strength and exhausts a card each turn
[HarmonyPatch]
public sealed class ToastyMittensStats : SimpleCounterStats<ToastyMittens>
{
    public override string Format => "Gained {0} [gold]Strength[/gold] and exhausted cards.";

    // Both versions name their Player parameter "player", so one postfix covers either.
    public static IEnumerable<MethodBase> TargetMethods() =>
        PatchTarget.FirstDeclared(typeof(ToastyMittens),
            nameof(ToastyMittens.AfterPlayerTurnStart), nameof(ToastyMittens.BeforeHandDraw));

    public static void Postfix(ToastyMittens __instance, Player player)
    {
        if (player != __instance.Owner.Creature.Player) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Strength.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // 0.111: AfterPlayerTurnStart exhausts one card from the hand (a selection prompt when the
        // hand has cards; the test hand is empty) and then applies Strength. The stat counts the
        // relic's Strength at method entry, once per turn. The auto-selector is pushed before the
        // fight so a prompt, if one opens, never blocks the turn end.
        runner.Do("auto-answer prompts + add relic", () => { TestHelpers.PushAutoCardSelector(); TestHelpers.AddRelic(RelicId); });
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked turn-1 strength", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Strength.IntValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected Amount == {expected}, got {Amount}");
        });
        runner.Do("god mode + protect enemy + end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked turn-2 strength", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = 2 * relic!.DynamicVars.Strength.IntValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected Amount == {expected} after two turns, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.PopCardSelector(); TestHelpers.CloseOverlays(); TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// WarHammer: upgrades cards after elite victories
[HarmonyPatch(typeof(WarHammer), nameof(WarHammer.AfterCombatVictory))]
public sealed class WarHammerStats : SimpleCounterStats<WarHammer>
{
    public override string Format => "Upgraded {0} cards after elite combats.";
    public override StatCadence Cadence => StatCadence.Combat;
    [ThreadStatic] private static int _willUpgrade;

    // The relic shuffles the deck's upgradable cards and Take()s DynamicVars.Cards of them, so a
    // small deck upgrades fewer than the nominal count. Count what Take will yield before it runs.
    public static void Prefix(WarHammer __instance, CombatRoom room)
    {
        _willUpgrade = 0;
        if (room.RoomType != RoomType.Elite) return;
        int upgradable = PileType.Deck.GetPile(__instance.Owner).Cards.Count(c => c.IsUpgradable);
        _willUpgrade = Math.Min(__instance.DynamicVars.Cards.IntValue, upgradable);
    }

    public static void Postfix(WarHammer __instance)
    {
        if (_willUpgrade <= 0) return;
        Track(__instance, s => s.Amount += _willUpgrade);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // The test deck is empty, so two added Strikes are the only upgradable cards: the relic
        // upgrades exactly those two, not its nominal Cards (4).
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start elite fight", () => TestHelpers.StartEliteFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("add first upgradable card to the deck", () => TestHelpers.AddCardToDeck("STRIKE_IRONCLAD"));
        runner.WaitFor(GameEvent.CardChangedPiles);
        runner.Do("add second upgradable card to the deck", () => TestHelpers.AddCardToDeck("STRIKE_IRONCLAD"));
        runner.WaitFor(GameEvent.CardChangedPiles);
        runner.Assert("nothing before the victory", () =>
            new TestResult(Amount == 0, $"expected Amount == 0 before the win, got {Amount}"));
        runner.Do("win combat", () => TestHelpers.WinCombat());
        runner.WaitFor(GameEvent.CombatVictory);
        // The Monster-room guard is not exercised: that would need a second full combat.
        runner.Assert("upgraded exactly the two upgradable cards", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var nominal = relic?.DynamicVars.Cards.IntValue ?? -1;
            return new TestResult(nominal > 2 && Amount == 2, $"expected Amount == 2 (nominal {nominal}, 2 upgradable), got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// WongosMysteryTicket: tracks combats toward relic reward
[HarmonyPatch(typeof(WongosMysteryTicket), nameof(WongosMysteryTicket.AfterCombatEnd))]
public sealed class WongosMysteryTicketStats : SimpleCounterStats<WongosMysteryTicket>
{
    public override string Format => "Completed {0} combats toward relic.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(WongosMysteryTicket __instance) =>
        Track(__instance, s => s.Amount++);

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // AfterCombatEnd fires when combat ends.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("win combat", () => TestHelpers.WinCombat());
        runner.WaitFor(GameEvent.CombatEnd);
        runner.Assert("tracked combat completion", () =>
            new TestResult(Amount == 1, $"expected Amount == 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// BigHat: generates Ethereal cards on turn 1
[HarmonyPatch(typeof(BigHat), nameof(BigHat.AfterSideTurnStart))]
public sealed class BigHatStats : SimpleCounterStats<BigHat>
{
    public override string Format => "Generated {0} [gold]Ethereal[/gold] cards.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(BigHat __instance, CombatSide side, ICombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (__instance.Owner.PlayerCombatState!.TurnNumber > 1) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Cards.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.SideTurnStart);
        runner.Assert("tracked cards", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Cards.IntValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected Amount == {expected}, got {Amount}");
        });
        // Turn 2 is past the turn-1 guard. The player's turn-2 AfterSideTurnStart fires after
        // PlayerTurnStart, so wait for that SideTurnStart before asserting.
        runner.Do("god mode + protect enemy + end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.WaitFor(GameEvent.SideTurnStart, 15000);
        runner.Assert("still Cards on turn 2", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Cards.IntValue;
            return new TestResult(Amount == expected, $"expected Amount still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// BingBong: duplicates cards added to deck
[HarmonyPatch(typeof(BingBong), nameof(BingBong.AfterCardChangedPiles))]
public sealed class BingBongStats : SimpleCounterStats<BingBong>
{
    public override string Format => "Duplicated {0} cards.";
    public override StatCadence Cadence => StatCadence.Total;
    private static readonly FieldInfo _cardsToSkipField =
        AccessTools.Field(typeof(BingBong), "_cardsToSkip");

    // The decision lives in Harmony's per-call __state, not a static: adding the clone re-enters
    // AfterCardChangedPiles (for the clone, which is skipped), and a shared static was reset by that
    // nested call before the outer call's Postfix read it, so no duplication was ever counted.
    public static void Prefix(BingBong __instance, CardModel card, AbstractModel? clonedBy, out bool __state)
    {
        __state = false;
        CardPile? pile = card.Pile;
        if (pile == null || pile.Type != PileType.Deck) return;
        if (card.Owner != __instance.Owner) return;
        if (clonedBy != null) return;
        var skip = (HashSet<CardModel>?)_cardsToSkipField.GetValue(__instance);
        if (skip != null && skip.Contains(card)) return;
        __state = true;
    }

    public static void Postfix(BingBong __instance, bool __state)
    {
        if (!__state) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // A card entering the Deck pile with no cloner is duplicated; the clone is added with
        // clonedBy set and sits in _cardsToSkip, so it is not duplicated again. Both adds fire
        // AfterCardChangedPiles (the clone's is nested inside the original's), so wait for both
        // before asserting: the second signal is the one after which the tracker has run in
        // either nesting order.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.CombatStart);
        runner.Do("add a card to the deck", () => TestHelpers.AddCardToDeck("STRIKE_IRONCLAD"));
        runner.WaitFor(GameEvent.CardChangedPiles);
        runner.WaitFor(GameEvent.CardChangedPiles, 15000);
        runner.Assert("tracked one duplication; deck holds the card and its clone", () => {
            int strikes = PileType.Deck.GetPile(TestHelpers.Player!).Cards.Count(c => c.Id.Entry == "STRIKE_IRONCLAD");
            return new TestResult(Amount == 1 && strikes == 2, $"expected Amount == 1 and 2 Strikes in the deck, got Amount {Amount}, Strikes {strikes}");
        });
        // A card entering a combat pile (not the Deck) does not count.
        runner.Do("add a card to the hand pile", () => TestHelpers.AddCardToCombatPile("STRIKE_IRONCLAD", "Hand"));
        runner.WaitFor(GameEvent.CardChangedPiles);
        runner.Assert("still 1 after a hand add", () => {
            int strikes = PileType.Deck.GetPile(TestHelpers.Player!).Cards.Count(c => c.Id.Entry == "STRIKE_IRONCLAD");
            return new TestResult(Amount == 1 && strikes == 2, $"expected Amount still 1 and 2 Strikes in the deck, got Amount {Amount}, Strikes {strikes}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// VexingPuzzlebox: generates a free card on turn 1
[HarmonyPatch(typeof(VexingPuzzlebox), nameof(VexingPuzzlebox.AfterPlayerTurnStart))]
public sealed class VexingPuzzleboxStats : SimpleCounterStats<VexingPuzzlebox>
{
    public override string Format => "Generated {0} free cards.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(VexingPuzzlebox __instance, Player player)
    {
        if (player != __instance.Owner) return;
        if (__instance.Owner.PlayerCombatState!.TurnNumber != 1) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked stat", () =>
            new TestResult(Amount == 1, $"expected Amount == 1, got {Amount}"));
        // Turn 2 is past the turn-1 guard: the turn-2 AfterPlayerTurnStart has run by this signal.
        runner.Do("god mode + protect enemy + end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("still 1 on turn 2", () =>
            new TestResult(Amount == 1, $"expected Amount still 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// ChoicesParadox: generates cards to choose from on turn 1
[HarmonyPatch(typeof(ChoicesParadox), nameof(ChoicesParadox.AfterPlayerTurnStart))]
public sealed class ChoicesParadoxStats : SimpleCounterStats<ChoicesParadox>
{
    public override string Format => "Generated {0} cards to choose from.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(ChoicesParadox __instance, Player player)
    {
        if (player != __instance.Owner) return;
        if (__instance.Owner.PlayerCombatState!.TurnNumber != 1) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // The relic opens a pick-one grid (CardSelectCmd.FromSimpleGrid) on turn 1; the auto-selector
        // answers it so the turn can end.
        runner.Do("auto-answer prompts + add relic", () => { TestHelpers.PushAutoCardSelector(); TestHelpers.AddRelic(RelicId); });
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked stat", () =>
            new TestResult(Amount == 1, $"expected Amount == 1, got {Amount}"));
        // Turn 2 is past the turn-1 guard: the turn-2 AfterPlayerTurnStart has run by this signal.
        runner.Do("god mode + protect enemy + end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("still 1 on turn 2", () =>
            new TestResult(Amount == 1, $"expected Amount still 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.PopCardSelector(); TestHelpers.CloseOverlays(); TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// JeweledMask: draws a free Power on turn 1
[HarmonyPatch(typeof(JeweledMask), nameof(JeweledMask.BeforeHandDraw))]
public sealed class JeweledMaskStats : SimpleCounterStats<JeweledMask>
{
    public override string Format => "Drew {0} free Powers.";
    public override StatCadence Cadence => StatCadence.Total;
    // Prefix: the relic only acts when the draw pile holds a Power, and it moves that Power to the hand.
    public static void Prefix(JeweledMask __instance, Player player)
    {
        if (player != __instance.Owner) return;
        if (__instance.Owner.PlayerCombatState!.TurnNumber > 1) return;
        if (!PileType.Draw.GetPile(player).Cards.Any(c => c.Type == CardType.Power)) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // The draw pile is built at combat start, so the Power must be in the deck before the fight.
        // First fight with an empty deck: no Power to draw, nothing counted.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight with an empty deck", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("no Power in the draw pile, nothing counted", () =>
            new TestResult(Amount == 0, $"expected Amount == 0, got {Amount}"));
        runner.Do("add Demon Form to deck", () => TestHelpers.AddCardToDeck("DEMON_FORM"));
        runner.WaitFor(GameEvent.CardChangedPiles);
        runner.Do("start fight with a Power in the deck", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked the free Power", () =>
            new TestResult(Amount == 1, $"expected Amount == 1, got {Amount}"));
        // Turn 2 is past the turn-1 guard: its hand draw has run by the turn-2 PlayerTurnStart.
        runner.Do("god mode + protect enemy + end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("still 1 on turn 2", () =>
            new TestResult(Amount == 1, $"expected Amount still 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// VelvetChoker: tracks times card limit was hit
[HarmonyPatch(typeof(VelvetChoker), nameof(VelvetChoker.AfterCardPlayed))]
public sealed class VelvetChokerStats : SimpleCounterStats<VelvetChoker>
{
    public override string Format => "Hit card limit {0} times.";
    private static readonly FieldInfo _cardsPlayedField =
        AccessTools.Field(typeof(VelvetChoker), "_cardsPlayedThisTurn");

    public static void Postfix(VelvetChoker __instance, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != __instance.Owner) return;
        int cardsPlayed = (int)_cardsPlayedField.GetValue(__instance)!;
        if (cardsPlayed != __instance.DynamicVars.Cards.IntValue) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // The stat counts the play on which _cardsPlayedThisTurn reaches DynamicVars.Cards (6):
        // five Shivs are one short, the sixth is the hit. Shivs exhaust, so index 0 is always next.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("energy + god mode + protect enemy + 6 shivs", () => {
            TestHelpers.AddEnergy(10);
            TestHelpers.EnableGodMode();
            TestHelpers.ProtectEnemy();
            for (int i = 0; i < 6; i++) TestHelpers.SpawnCard("SHIV");
        });
        for (int n = 1; n <= 5; n++)
        {
            runner.Do($"play shiv {n}", () => TestHelpers.PlayCard(0, 0));
            runner.WaitFor(GameEvent.CardPlayed);
        }
        runner.Assert("nothing after 5 of 6 cards", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var limit = relic?.DynamicVars.Cards.IntValue ?? -1;
            return new TestResult(limit == 6 && Amount == 0, $"expected Amount == 0 after 5 plays (limit {limit}), got {Amount}");
        });
        runner.Do("play shiv 6 + end turn", () => TestHelpers.PlayThenEndTurn(1, 0));
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked the card limit on the 6th play", () =>
            new TestResult(Amount == 1, $"expected Amount == 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// DiamondDiadem: redesigned in 0.110 — was a power applied at turn end when few cards had been
// played, now Block granted on the first turn. Tracked quantity and label follow the version.
public sealed class DiamondDiademStats : SimpleCounterStats<DiamondDiadem>
{
    internal static readonly bool GrantsBlock =
        AccessTools.DeclaredMethod(typeof(DiamondDiadem), nameof(DiamondDiadem.AfterSideTurnStart)) != null;

    public override string Format => GrantsBlock
        ? "Granted {0} [gold]Block[/gold]."
        : "Applied [gold]DiamondDiademPower[/gold] {0} times.";
    public override StatCadence Cadence => StatCadence.Total;

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // 0.110+: AfterSideTurnStart grants Block on the owner's first turn only; the player's
        // SideTurnStart is its sync point (it fires after PlayerTurnStart). 0.107.1 counted an
        // activation at each turn end instead, so the expected value follows the version switch.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.SideTurnStart);
        runner.Assert("tracked first-turn block", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = GrantsBlock ? relic!.DynamicVars.Block.IntValue : 0;
            return new TestResult((!GrantsBlock || expected > 0) && Amount == expected, $"expected Amount == {expected}, got {Amount}");
        });
        // Turn 2 is past the turn-1 guard (0.110+) / is one more activation (0.107.1).
        runner.Do("god mode + protect enemy + end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.WaitFor(GameEvent.SideTurnStart, 15000);
        runner.Assert("still first-turn block on turn 2", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = GrantsBlock ? relic!.DynamicVars.Block.IntValue : 1;
            return new TestResult(Amount == expected, $"expected Amount still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// 0.110+: grants Block at the start of the owner's first turn.
[HarmonyPatch]
internal static class DiamondDiademAfterSideTurnStartPatch
{
    public static IEnumerable<MethodBase> TargetMethods() =>
        PatchTarget.DeclaredOrNone(typeof(DiamondDiadem), nameof(DiamondDiadem.AfterSideTurnStart));

    public static void Postfix(DiamondDiadem __instance, IReadOnlyList<Creature> participants)
    {
        if (!participants.Contains(__instance.Owner.Creature)) return;
        if (__instance.Owner.PlayerCombatState!.TurnNumber > 1) return;
        DiamondDiademStats.Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
    }
}

// 0.107.1: the relic zeroes its own counter inside the hook, so decide in a Prefix.
// CardsPlayedThisTurn no longer exists in 0.110, hence the reflection.
[HarmonyPatch]
internal static class DiamondDiademBeforeSideTurnEndPatch
{
    private static readonly PropertyInfo? CardsPlayedThisTurn =
        AccessTools.Property(typeof(DiamondDiadem), "CardsPlayedThisTurn");
    [ThreadStatic] private static bool _willApply;

    public static IEnumerable<MethodBase> TargetMethods() =>
        PatchTarget.DeclaredOrNone(typeof(DiamondDiadem), nameof(DiamondDiadem.BeforeSideTurnEnd));

    public static void Prefix(DiamondDiadem __instance, IEnumerable<Creature> participants)
    {
        _willApply = false;
        if (CardsPlayedThisTurn == null) return;
        if (!participants.Contains(__instance.Owner.Creature)) return;
        var played = (int)CardsPlayedThisTurn.GetValue(__instance)!;
        _willApply = played <= __instance.DynamicVars["CardThreshold"].BaseValue;
    }

    public static void Postfix(DiamondDiadem __instance)
    {
        if (!_willApply) return;
        DiamondDiademStats.Track(__instance, s => s.Amount++);
    }
}

// BeltBuckle: grants Dexterity when no potions held
[HarmonyPatch(typeof(BeltBuckle), nameof(BeltBuckle.BeforeCombatStart))]
public sealed class BeltBuckleStats : SimpleCounterStats<BeltBuckle>
{
    public override string Format => "Granted {0} [gold]Dexterity[/gold].";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(BeltBuckle __instance)
    {
        if (__instance.Owner.Potions.Any()) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Dexterity.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => { TestHelpers.ClearPotions(); TestHelpers.AddRelic(RelicId); });
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.CombatStart);
        runner.Assert("tracked dexterity", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Dexterity.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// ── New relics (0.109.0) ───────────────────────────────────────────────

// FishingRod: every N combats, upgrades a random card in the deck
[HarmonyPatch(typeof(FishingRod), nameof(FishingRod.AfterCombatEnd))]
public sealed class FishingRodStats : SimpleCounterStats<FishingRod>
{
    [System.ThreadStatic] private static bool _willUpgrade;
    public override string Format => "Upgraded {0} cards.";
    public override StatCadence Cadence => StatCadence.Total;

    public static void Prefix(FishingRod __instance, CombatRoom room)
    {
        _willUpgrade = false;
        if (room.Encounter.RoomType != RoomType.Monster) return;
        int combats = __instance.DynamicVars["Combats"].IntValue;
        if (combats <= 0) return;
        // CombatsSeen is incremented inside the method; it upgrades when the new count hits the interval
        if ((__instance.CombatsSeen + 1) % combats != 0) return;
        _willUpgrade = PileType.Deck.GetPile(__instance.Owner).Cards.Any(c => c.IsUpgradable);
    }

    public static void Postfix(FishingRod __instance)
    {
        if (!_willUpgrade) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // Upgrades a random upgradable deck card when CombatsSeen reaches a multiple of "Combats"
        // after a Monster combat. Give the empty test deck an upgradable card and put the counter
        // one combat short of the interval, then win.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("add an upgradable card to the deck", () => TestHelpers.AddCardToDeck("STRIKE_IRONCLAD"));
        runner.WaitFor(GameEvent.CardChangedPiles);
        runner.Do("set the counter one combat short of the interval", () => {
            var relic = TestHelpers.GetRelic<FishingRod>() ?? throw new InvalidOperationException("Fishing Rod is not on the player");
            relic.CombatsSeen = relic.DynamicVars["Combats"].IntValue - 1;
        });
        runner.Assert("nothing before the combat ends", () =>
            new TestResult(Amount == 0, $"expected Amount == 0 before the win, got {Amount}"));
        runner.Do("win combat", () => TestHelpers.WinCombat());
        runner.WaitFor(GameEvent.CombatEnd, 15000);
        runner.Assert("tracked the interval upgrade", () =>
            new TestResult(Amount == 1, $"expected Amount == 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// WingedBoots: grants up to 3 free map travels
[HarmonyPatch(typeof(WingedBoots), nameof(WingedBoots.AfterRoomEntered))]
public sealed class WingedBootsStats : SimpleCounterStats<WingedBoots>
{
    [System.ThreadStatic] private static int _before;
    public override string Format => "Used {0} free travels.";
    public override StatCadence Cadence => StatCadence.Total;

    public static void Prefix(WingedBoots __instance) => _before = __instance.TimesUsed;

    public static void Postfix(WingedBoots __instance)
    {
        int delta = __instance.TimesUsed - _before;
        if (delta <= 0) return;
        Track(__instance, s => s.Amount += delta);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // AfterRoomEntered counts when the current map point is not a child of the previously
        // visited one (with >= 2 visited coords, a single room at the point, and uses left).
        // TravelTo records the coord the way the debug map does; AddVisitedMapCoord ignores a coord
        // already visited, so only unvisited points are chosen. The visited coords and ActFloor are
        // snapshotted first and restored in Cleanup, so the live save is left as it was and repeated
        // runs see the same map state.
        static MegaCrit.Sts2.Core.Runs.RunState Run() =>
            TestHelpers.Player?.RunState as MegaCrit.Sts2.Core.Runs.RunState
            ?? throw new InvalidOperationException("no RunState on the player");
        List<MegaCrit.Sts2.Core.Map.MapCoord>? savedCoords = null;
        int savedActFloor = 0;
        // Seed a position before the relic exists, so this move can never count.
        runner.Do("snapshot + seed the map position", () => {
            var run = Run();
            savedCoords = run.VisitedMapCoords.ToList();
            savedActFloor = run.ActFloor;
            var start = run.Map.GetPointsInRow(0).FirstOrDefault(p => !run.VisitedMapCoords.Contains(p.coord))
                ?? run.CurrentMapPoint
                ?? throw new InvalidOperationException("no map point to start from");
            TestHelpers.TravelTo(start, RoomType.RestSite);
        });
        runner.WaitFor(GameEvent.RoomEntered, 15000);
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        // An adjacent move (a child of the previous point) is not a free travel.
        runner.Do("travel to an adjacent point", () => {
            var run = Run();
            var cur = run.CurrentMapPoint ?? throw new InvalidOperationException("no current map point");
            var child = cur.Children.FirstOrDefault(p => !run.VisitedMapCoords.Contains(p.coord))
                ?? throw new InvalidOperationException($"every child of {cur.coord} is already visited");
            TestHelpers.TravelTo(child, RoomType.RestSite);
        });
        runner.WaitFor(GameEvent.RoomEntered, 15000);
        runner.Assert("adjacent travel is not counted", () =>
            new TestResult(Amount == 0, $"expected Amount == 0 after an adjacent move, got {Amount}"));
        // Two rows down is never a child of the previous point.
        runner.Do("jump two rows down", () => {
            var run = Run();
            var cur = run.CurrentMapPoint ?? throw new InvalidOperationException("no current map point");
            var target = run.Map.GetPointsInRow(cur.coord.row + 2).FirstOrDefault(p => !run.VisitedMapCoords.Contains(p.coord))
                ?? throw new InvalidOperationException($"no unvisited point in row {cur.coord.row + 2}");
            TestHelpers.TravelTo(target, RoomType.RestSite);
        });
        runner.WaitFor(GameEvent.RoomEntered, 15000);
        runner.Assert("tracked one free travel", () =>
            new TestResult(Amount == 1, $"expected Amount == 1 after a non-adjacent jump, got {Amount}"));
        runner.Cleanup(() =>
        {
            TestHelpers.RemoveRelic(RelicId);
            Reset();
            if (savedCoords == null) return;
            var run = Run();
            run.ClearVisitedMapCoordsDebug();
            foreach (var coord in savedCoords) run.AddVisitedMapCoord(coord);
            run.ActFloor = savedActFloor;
        });
    }
#endif
}

// NOTE: NeowsSacrifice is a 0.108/0.109-beta-only relic; omitted so the mod loads on stable (0.107.1).
