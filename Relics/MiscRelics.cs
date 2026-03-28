using System.Linq;
using System.Text.Json.Nodes;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using RelicStats.Core;

namespace RelicStats.Relics;

// BlackStar: extra relic reward from elites
[HarmonyPatch(typeof(BlackStar), nameof(BlackStar.TryModifyRewards))]
public sealed class BlackStarStats : SimpleCounterStats<BlackStar>
{
    public override string Format => "Gained {0} extra relic rewards.";
    public static void Postfix(BlackStar __instance, bool __result)
    {
        if (!__result) return;
        Track(__instance, s => s.Amount++);
    }
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
}

// BurningSticks: duplicates first exhausted skill per combat
[HarmonyPatch(typeof(BurningSticks), nameof(BurningSticks.AfterCardExhausted))]
public sealed class BurningSticksStats : SimpleCounterStats<BurningSticks>
{
    public override string Format => "Duplicated {0} cards.";
    private static readonly System.Reflection.FieldInfo _wasUsedField =
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
}

// CrackedCore: channels lightning at combat start
[HarmonyPatch(typeof(CrackedCore), nameof(CrackedCore.BeforeSideTurnStart))]
public sealed class CrackedCoreStats : SimpleCounterStats<CrackedCore>
{
    public override string Format => "Channeled {0} [gold]Lightning[/gold] orbs.";
    public static void Postfix(CrackedCore __instance, CombatSide side, CombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (combatState.RoundNumber > 1) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars["Lightning"].IntValue);
    }
}

// InfusedCore: channels lightning at combat start (upgraded variant)
[HarmonyPatch(typeof(InfusedCore), nameof(InfusedCore.AfterSideTurnStart))]
public sealed class InfusedCoreStats : SimpleCounterStats<InfusedCore>
{
    public override string Format => "Channeled {0} [gold]Lightning[/gold] orbs.";
    public static void Postfix(InfusedCore __instance, CombatSide side, CombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (combatState.RoundNumber > 1) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars["Lightning"].IntValue);
    }
}

// DelicateFrond: generates potions before combat
[HarmonyPatch(typeof(DelicateFrond), nameof(DelicateFrond.BeforeCombatStart))]
public sealed class DelicateFrondStats : SimpleCounterStats<DelicateFrond>
{
    public override string Format => "Generated potions {0} times.";
    public static void Postfix(DelicateFrond __instance) =>
        Track(__instance, s => s.Amount++);
}

// DivineRight: gains stars at combat start
[HarmonyPatch(typeof(DivineRight), nameof(DivineRight.AfterRoomEntered))]
public sealed class DivineRightStats : SimpleCounterStats<DivineRight>
{
    public override string Format => "Gained {0} [gold]Stars[/gold].";
    public static void Postfix(DivineRight __instance, AbstractRoom room)
    {
        if (room is not CombatRoom) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Stars.IntValue);
    }
}

// FresnelLens: enchants cards with Nimble
[HarmonyPatch(typeof(FresnelLens), nameof(FresnelLens.TryModifyCardBeingAddedToDeck))]
public sealed class FresnelLensStats : SimpleCounterStats<FresnelLens>
{
    public override string Format => "Enchanted {0} cards.";
    public static void Postfix(FresnelLens __instance, bool __result)
    {
        if (!__result) return;
        Track(__instance, s => s.Amount++);
    }
}

// LavaLamp: upgrades card rewards when no damage taken
[HarmonyPatch(typeof(LavaLamp), nameof(LavaLamp.TryModifyCardRewardOptionsLate))]
public sealed class LavaLampStats : SimpleCounterStats<LavaLamp>
{
    public override string Format => "Upgraded card rewards {0} times.";
    public static void Postfix(LavaLamp __instance, bool __result)
    {
        if (!__result) return;
        Track(__instance, s => s.Amount++);
    }
}

// LunarPastry: gains stars at end of each turn
[HarmonyPatch(typeof(LunarPastry), nameof(LunarPastry.AfterTurnEnd))]
public sealed class LunarPastryStats : SimpleCounterStats<LunarPastry>
{
    public override string Format => "Gained {0} [gold]Stars[/gold].";
    public static void Postfix(LunarPastry __instance, CombatSide side)
    {
        if (side != __instance.Owner.Creature.Side) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Stars.IntValue);
    }
}

// MoltenEgg: auto-upgrades attack cards added to deck
[HarmonyPatch(typeof(MoltenEgg), nameof(MoltenEgg.TryModifyCardBeingAddedToDeck))]
public sealed class MoltenEggStats : SimpleCounterStats<MoltenEgg>
{
    public override string Format => "Upgraded {0} attack cards.";
    public static void Postfix(MoltenEgg __instance, bool __result)
    {
        if (!__result) return;
        Track(__instance, s => s.Amount++);
    }
}

// ToxicEgg: auto-upgrades skill cards added to deck
[HarmonyPatch(typeof(ToxicEgg), nameof(ToxicEgg.TryModifyCardBeingAddedToDeck))]
public sealed class ToxicEggStats : SimpleCounterStats<ToxicEgg>
{
    public override string Format => "Upgraded {0} skill cards.";
    public static void Postfix(ToxicEgg __instance, bool __result)
    {
        if (!__result) return;
        Track(__instance, s => s.Amount++);
    }
}

// FrozenEgg: auto-upgrades power cards added to deck
[HarmonyPatch(typeof(FrozenEgg), nameof(FrozenEgg.TryModifyCardBeingAddedToDeck))]
public sealed class FrozenEggStats : SimpleCounterStats<FrozenEgg>
{
    public override string Format => "Upgraded {0} power cards.";
    public static void Postfix(FrozenEgg __instance, bool __result)
    {
        if (!__result) return;
        Track(__instance, s => s.Amount++);
    }
}

// MummifiedHand: makes a card free when playing powers
[HarmonyPatch(typeof(MummifiedHand), nameof(MummifiedHand.AfterCardPlayed))]
public sealed class MummifiedHandStats : SimpleCounterStats<MummifiedHand>
{
    public override string Format => "Made {0} cards free.";
    public static void Postfix(MummifiedHand __instance, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != __instance.Owner) return;
        if (cardPlay.Card.Type != CardType.Power) return;
        if (!CombatManager.Instance.IsInProgress) return;
        Track(__instance, s => s.Amount++);
    }
}

// PaelsEye: grants an extra turn if no cards played, exhausts hand
// Complex: tracks extra turns taken and cards exhausted
[HarmonyPatch]
public sealed class PaelsEyeStats : IRelicStats
{
    public string RelicId => RelicIdHelper.Slugify(nameof(PaelsEye));
    public int TurnWhenObtained { get; set; }
    public int CombatWhenObtained { get; set; }
    public int? FrozenTurnCount { get; set; }
    public int? FrozenCombatCount { get; set; }

    public int ExtraTurns { get; set; }
    public int CardsExhausted { get; set; }

    public string GetDescription(int totalTurns, int totalCombats)
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
            ["turnObtained"] = TurnWhenObtained,
            ["combatObtained"] = CombatWhenObtained,
        };
        if (FrozenTurnCount.HasValue) obj["frozenTurns"] = FrozenTurnCount.Value;
        if (FrozenCombatCount.HasValue) obj["frozenCombats"] = FrozenCombatCount.Value;
        return obj;
    }

    public void Load(JsonObject data)
    {
        ExtraTurns = data["extraTurns"]?.GetValue<int>() ?? 0;
        CardsExhausted = data["cardsExhausted"]?.GetValue<int>() ?? 0;
        TurnWhenObtained = data["turnObtained"]?.GetValue<int>() ?? 0;
        CombatWhenObtained = data["combatObtained"]?.GetValue<int>() ?? 0;
        FrozenTurnCount = data["frozenTurns"]?.GetValue<int>();
        FrozenCombatCount = data["frozenCombats"]?.GetValue<int>();
    }

    public void Reset()
    {
        ExtraTurns = 0;
        CardsExhausted = 0;
        TurnWhenObtained = RelicStatsRegistry.TurnCount;
        CombatWhenObtained = RelicStatsRegistry.CombatCount;
        FrozenTurnCount = null;
        FrozenCombatCount = null;
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

    [HarmonyPatch(typeof(PaelsEye), nameof(PaelsEye.BeforeTurnEndEarly))]
    [HarmonyPrefix]
    public static void BeforeTurnEndEarlyPrefix(PaelsEye __instance, CombatSide side)
    {
        if (side != CombatSide.Player) return;
        // The method exhausts hand cards only when !UsedThisCombat && !AnyCardsPlayedThisTurn
        var usedField = AccessTools.Field(typeof(PaelsEye), "_usedThisCombat");
        var playedField = AccessTools.Field(typeof(PaelsEye), "_anyCardsPlayedThisTurn");
        if ((bool)usedField.GetValue(__instance)!) return;
        if ((bool)playedField.GetValue(__instance)!) return;
        if (!TryGet(__instance, out var stats)) return;
        var cards = CardPile.GetCards(__instance.Owner, PileType.Hand);
        stats.CardsExhausted += cards.Count();
    }
}

// PaelsWing: sacrifice card rewards for relics
[HarmonyPatch(typeof(PaelsWing), nameof(PaelsWing.OnSacrifice))]
public sealed class PaelsWingStats : SimpleCounterStats<PaelsWing>
{
    public override string Format => "Sacrificed {0} card rewards.";
    public static void Postfix(PaelsWing __instance) =>
        Track(__instance, s => s.Amount++);
}

// PenNib: triggers every 10 attacks, doubling damage
// Complex: tracks trigger count and total attacks
[HarmonyPatch]
public sealed class PenNibStats : IRelicStats
{
    public string RelicId => RelicIdHelper.Slugify(nameof(PenNib));
    public int TurnWhenObtained { get; set; }
    public int CombatWhenObtained { get; set; }
    public int? FrozenTurnCount { get; set; }
    public int? FrozenCombatCount { get; set; }

    public int Triggers { get; set; }
    public int AttacksPlayed { get; set; }

    public string GetDescription(int totalTurns, int totalCombats)
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
            ["turnObtained"] = TurnWhenObtained,
            ["combatObtained"] = CombatWhenObtained,
        };
        if (FrozenTurnCount.HasValue) obj["frozenTurns"] = FrozenTurnCount.Value;
        if (FrozenCombatCount.HasValue) obj["frozenCombats"] = FrozenCombatCount.Value;
        return obj;
    }

    public void Load(JsonObject data)
    {
        Triggers = data["triggers"]?.GetValue<int>() ?? 0;
        AttacksPlayed = data["attacksPlayed"]?.GetValue<int>() ?? 0;
        TurnWhenObtained = data["turnObtained"]?.GetValue<int>() ?? 0;
        CombatWhenObtained = data["combatObtained"]?.GetValue<int>() ?? 0;
        FrozenTurnCount = data["frozenTurns"]?.GetValue<int>();
        FrozenCombatCount = data["frozenCombats"]?.GetValue<int>();
    }

    public void Reset()
    {
        Triggers = 0;
        AttacksPlayed = 0;
        TurnWhenObtained = RelicStatsRegistry.TurnCount;
        CombatWhenObtained = RelicStatsRegistry.CombatCount;
        FrozenTurnCount = null;
        FrozenCombatCount = null;
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
}

// PhylacteryUnbound: summons minions at combat start and each turn
// Complex: tracks combat start summons and turn summons separately
[HarmonyPatch]
public sealed class PhylacteryUnboundStats : IRelicStats
{
    public string RelicId => RelicIdHelper.Slugify(nameof(PhylacteryUnbound));
    public int TurnWhenObtained { get; set; }
    public int CombatWhenObtained { get; set; }
    public int? FrozenTurnCount { get; set; }
    public int? FrozenCombatCount { get; set; }

    public int CombatStartSummons { get; set; }
    public int TurnSummons { get; set; }

    public string GetDescription(int totalTurns, int totalCombats)
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
            ["turnObtained"] = TurnWhenObtained,
            ["combatObtained"] = CombatWhenObtained,
        };
        if (FrozenTurnCount.HasValue) obj["frozenTurns"] = FrozenTurnCount.Value;
        if (FrozenCombatCount.HasValue) obj["frozenCombats"] = FrozenCombatCount.Value;
        return obj;
    }

    public void Load(JsonObject data)
    {
        CombatStartSummons = data["combatStartSummons"]?.GetValue<int>() ?? 0;
        TurnSummons = data["turnSummons"]?.GetValue<int>() ?? 0;
        TurnWhenObtained = data["turnObtained"]?.GetValue<int>() ?? 0;
        CombatWhenObtained = data["combatObtained"]?.GetValue<int>() ?? 0;
        FrozenTurnCount = data["frozenTurns"]?.GetValue<int>();
        FrozenCombatCount = data["frozenCombats"]?.GetValue<int>();
    }

    public void Reset()
    {
        CombatStartSummons = 0;
        TurnSummons = 0;
        TurnWhenObtained = RelicStatsRegistry.TurnCount;
        CombatWhenObtained = RelicStatsRegistry.CombatCount;
        FrozenTurnCount = null;
        FrozenCombatCount = null;
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
}

// PrayerWheel: extra card reward from normal combats
[HarmonyPatch(typeof(PrayerWheel), nameof(PrayerWheel.TryModifyRewards))]
public sealed class PrayerWheelStats : SimpleCounterStats<PrayerWheel>
{
    public override string Format => "Added {0} extra card rewards.";
    public static void Postfix(PrayerWheel __instance, bool __result)
    {
        if (!__result) return;
        Track(__instance, s => s.Amount++);
    }
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
}

// RedMask: applies weakness to all enemies at combat start
[HarmonyPatch(typeof(RedMask), nameof(RedMask.BeforeSideTurnStart))]
public sealed class RedMaskStats : SimpleCounterStats<RedMask>
{
    public override string Format => "Applied weakness {0} times.";
    public static void Postfix(RedMask __instance, CombatSide side, CombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (combatState.RoundNumber > 1) return;
        Track(__instance, s => s.Amount++);
    }
}

// RuinedHelmet: doubles first strength gain per combat
[HarmonyPatch(typeof(RuinedHelmet), nameof(RuinedHelmet.AfterModifyingPowerAmountReceived))]
public sealed class RuinedHelmetStats : SimpleCounterStats<RuinedHelmet>
{
    public override string Format => "Doubled strength {0} times.";
    public static void Postfix(RuinedHelmet __instance) =>
        Track(__instance, s => s.Amount++);
}

// Shovel: adds dig option at rest sites (track via TryModifyRestSiteOptions as proxy for availability)
// Since we can't patch DigRestSiteOption.OnSelect, we track times it offered the dig option
// This is a best-effort proxy; the player may not always choose to dig
[HarmonyPatch(typeof(Shovel), nameof(Shovel.TryModifyRestSiteOptions))]
public sealed class ShovelStats : SimpleCounterStats<Shovel>
{
    public override string Format => "Offered dig {0} times.";
    public static void Postfix(Shovel __instance, bool __result)
    {
        if (!__result) return;
        Track(__instance, s => s.Amount++);
    }
}

// SlingOfCourage: gains strength when entering elite rooms
[HarmonyPatch(typeof(SlingOfCourage), nameof(SlingOfCourage.AfterRoomEntered))]
public sealed class SlingOfCourageStats : SimpleCounterStats<SlingOfCourage>
{
    public override string Format => "Gained {0} [gold]Strength[/gold].";
    public static void Postfix(SlingOfCourage __instance, AbstractRoom room)
    {
        if (room.RoomType != RoomType.Elite) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Strength.IntValue);
    }
}

// Akabeko: gains vigor at start of first turn each combat
[HarmonyPatch(typeof(Akabeko), nameof(Akabeko.AfterSideTurnStart))]
public sealed class AkabekoStats : SimpleCounterStats<Akabeko>
{
    public override string Format => "Gained {0} [gold]Vigor[/gold].";
    public static void Postfix(Akabeko __instance, CombatSide side, CombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (combatState.RoundNumber > 1) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars["VigorPower"].IntValue);
    }
}

// MiniRegent: gains strength first time stars are spent each turn
[HarmonyPatch(typeof(MiniRegent), nameof(MiniRegent.AfterStarsSpent))]
public sealed class MiniRegentStats : SimpleCounterStats<MiniRegent>
{
    public override string Format => "Gained {0} [gold]Strength[/gold].";
    private static readonly System.Reflection.FieldInfo _usedThisTurnField =
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
}

// RoyalPoison: deals self-damage at start of first turn
[HarmonyPatch(typeof(RoyalPoison), nameof(RoyalPoison.AfterPlayerTurnStart))]
public sealed class RoyalPoisonStats : SimpleCounterStats<RoyalPoison>
{
    public override string Format => "Dealt {0} [gold]Damage[/gold] to self.";
    public static void Postfix(RoyalPoison __instance, Player player)
    {
        if (player != __instance.Owner) return;
        if (player.Creature.CombatState.RoundNumber > 1) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Damage.IntValue);
    }
}

// Shuriken: gains strength every 3 attacks played per turn
[HarmonyPatch(typeof(Shuriken), nameof(Shuriken.AfterCardPlayed))]
public sealed class ShurikenStats : SimpleCounterStats<Shuriken>
{
    public override string Format => "Gained {0} [gold]Strength[/gold].";
    private static readonly System.Reflection.FieldInfo _attacksField =
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
}

// Kunai: gains dexterity every 3 attacks played per turn
[HarmonyPatch(typeof(Kunai), nameof(Kunai.AfterCardPlayed))]
public sealed class KunaiStats : SimpleCounterStats<Kunai>
{
    public override string Format => "Gained {0} [gold]Dexterity[/gold].";
    private static readonly System.Reflection.FieldInfo _attacksField =
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
}

// Vajra: gains strength at combat start
[HarmonyPatch(typeof(Vajra), nameof(Vajra.AfterRoomEntered))]
public sealed class VajraStats : SimpleCounterStats<Vajra>
{
    public override string Format => "Gained {0} [gold]Strength[/gold].";
    public static void Postfix(Vajra __instance, AbstractRoom room)
    {
        if (room is not CombatRoom) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Strength.IntValue);
    }
}

// PetrifiedToad: generates a PotionShapedRock before each combat
[HarmonyPatch(typeof(PetrifiedToad), nameof(PetrifiedToad.BeforeCombatStartLate))]
public sealed class PetrifiedToadStats : SimpleCounterStats<PetrifiedToad>
{
    public override string Format => "Generated {0} potions.";
    public static void Postfix(PetrifiedToad __instance) =>
        Track(__instance, s => s.Amount++);
}

// Toolbox: offers colorless card choice at start of combat
[HarmonyPatch(typeof(Toolbox), nameof(Toolbox.BeforeHandDraw))]
public sealed class ToolboxStats : SimpleCounterStats<Toolbox>
{
    public override string Format => "Offered cards {0} times.";
    public static void Postfix(Toolbox __instance, Player player, CombatState combatState)
    {
        if (player != __instance.Owner) return;
        if (combatState.RoundNumber != 1) return;
        Track(__instance, s => s.Amount++);
    }
}

// DarkstonePeriapt: gains max HP when curses enter deck
[HarmonyPatch(typeof(DarkstonePeriapt), nameof(DarkstonePeriapt.AfterCardChangedPiles))]
public sealed class DarkstonePeriaptStats : SimpleCounterStats<DarkstonePeriapt>
{
    public override string Format => "Gained {0} max HP.";
    public static void Postfix(DarkstonePeriapt __instance, CardModel card)
    {
        CardPile? pile = card.Pile;
        if (pile == null || pile.Type != PileType.Deck) return;
        if (card.Owner != __instance.Owner) return;
        if (card.Type != CardType.Curse) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.MaxHp.IntValue);
    }
}

// Girya: gains strength at combat start based on times lifted
[HarmonyPatch(typeof(Girya), nameof(Girya.AfterRoomEntered))]
public sealed class GiryaStats : SimpleCounterStats<Girya>
{
    public override string Format => "Gained {0} [gold]Strength[/gold].";
    public static void Postfix(Girya __instance, AbstractRoom room)
    {
        if (__instance.TimesLifted <= 0) return;
        if (room is not CombatRoom) return;
        Track(__instance, s => s.Amount += __instance.TimesLifted);
    }
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
}

// SneckoSkull: adds extra poison to all poison applications
[HarmonyPatch(typeof(SneckoSkull), nameof(SneckoSkull.AfterModifyingPowerAmountGiven))]
public sealed class SneckoSkullStats : SimpleCounterStats<SneckoSkull>
{
    public override string Format => "Added {0} extra [gold]Poison[/gold].";
    public static void Postfix(SneckoSkull __instance) =>
        Track(__instance, s => s.Amount += __instance.DynamicVars.Poison.IntValue);
}

// TwistedFunnel: applies poison to all enemies at combat start
[HarmonyPatch(typeof(TwistedFunnel), nameof(TwistedFunnel.BeforeSideTurnStart))]
public sealed class TwistedFunnelStats : SimpleCounterStats<TwistedFunnel>
{
    public override string Format => "Applied {0} [gold]Poison[/gold].";
    public static void Postfix(TwistedFunnel __instance, CombatSide side, CombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (combatState.RoundNumber > 1) return;
        int enemies = __instance.Owner.Creature.CombatState.HittableEnemies.Count;
        Track(__instance, s => s.Amount += __instance.DynamicVars["PoisonPower"].IntValue * enemies);
    }
}

// Pendulum: draws a card on each shuffle
[HarmonyPatch(typeof(Pendulum), nameof(Pendulum.AfterShuffle))]
public sealed class PendulumStats : SimpleCounterStats<Pendulum>
{
    public override string Format => "Drew {0} cards.";
    public static void Postfix(Pendulum __instance, Player player)
    {
        if (player != __instance.Owner) return;
        Track(__instance, s => s.Amount++);
    }
}

// ChosenCheese: gains max HP at end of combat
[HarmonyPatch(typeof(ChosenCheese), nameof(ChosenCheese.AfterCombatEnd))]
public sealed class ChosenCheeseStats : SimpleCounterStats<ChosenCheese>
{
    public override string Format => "Gained {0} max HP.";
    protected override string FormatStat(int amount) => FormatStatGreen(amount);
    public static void Postfix(ChosenCheese __instance) =>
        Track(__instance, s => s.Amount += __instance.DynamicVars["MaxHp"].IntValue);
}

// BookOfFiveRings: heals when adding cards to deck
[HarmonyPatch(typeof(BookOfFiveRings), nameof(BookOfFiveRings.AfterCardChangedPiles))]
public sealed class BookOfFiveRingsStats : SimpleCounterStats<BookOfFiveRings>
{
    public override string Format => "Healed {0} HP from adding cards.";
    protected override string FormatStat(int amount) => FormatStatGreen(amount);

    [System.ThreadStatic] private static int _prevCardsAdded;

    public static void Prefix(BookOfFiveRings __instance)
    {
        // CardsAdded is a [SavedProperty], not a DynamicVar
        _prevCardsAdded = __instance.CardsAdded;
    }

    public static void Postfix(BookOfFiveRings __instance)
    {
        // Trigger happened when CardsAdded incremented and modulo wrapped to 0
        if (__instance.CardsAdded > _prevCardsAdded && __instance.CardsAdded % __instance.DynamicVars.Cards.IntValue == 0)
            Track(__instance, s => s.Amount += __instance.DynamicVars.Heal.IntValue);
    }
}

// BagOfMarbles: applies Vulnerable to all enemies at combat start
[HarmonyPatch(typeof(BagOfMarbles), nameof(BagOfMarbles.BeforeSideTurnStart))]
public sealed class BagOfMarblesStats : SimpleCounterStats<BagOfMarbles>
{
    public override string Format => "Applied [gold]Vulnerable[/gold] {0} times.";
    public static void Postfix(BagOfMarbles __instance, CombatSide side, CombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (combatState.RoundNumber > 1) return;
        Track(__instance, s => s.Amount++);
    }
}

// Bellows: upgrades hand on turn 1
[HarmonyPatch(typeof(Bellows), nameof(Bellows.AfterPlayerTurnStart))]
public sealed class BellowsStats : SimpleCounterStats<Bellows>
{
    public override string Format => "Upgraded {0} hands.";
    public static void Postfix(Bellows __instance, Player player)
    {
        if (player != __instance.Owner) return;
        if (player.Creature.CombatState.RoundNumber > 1) return;
        Track(__instance, s => s.Amount++);
    }
}

// BronzeScales: applies Thorns at combat start
[HarmonyPatch(typeof(BronzeScales), nameof(BronzeScales.AfterRoomEntered))]
public sealed class BronzeScalesStats : SimpleCounterStats<BronzeScales>
{
    public override string Format => "Applied {0} [gold]Thorns[/gold].";
    public static void Postfix(BronzeScales __instance, AbstractRoom room)
    {
        if (room is not CombatRoom) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars["ThornsPower"].IntValue);
    }
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
}

// DataDisk: applies Focus at combat start
[HarmonyPatch(typeof(DataDisk), nameof(DataDisk.AfterRoomEntered))]
public sealed class DataDiskStats : SimpleCounterStats<DataDisk>
{
    public override string Format => "Applied {0} [gold]Focus[/gold].";
    public static void Postfix(DataDisk __instance, AbstractRoom room)
    {
        if (room is not CombatRoom) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars["FocusPower"].IntValue);
    }
}

// EmberTea: applies Strength at combat start (limited uses)
[HarmonyPatch(typeof(EmberTea), nameof(EmberTea.AfterRoomEntered))]
public sealed class EmberTeaStats : SimpleCounterStats<EmberTea>
{
    public override string Format => "Applied {0} [gold]Strength[/gold].";
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
}

// FakeSneckoEye: applies Confused at combat start
[HarmonyPatch(typeof(FakeSneckoEye), nameof(FakeSneckoEye.BeforeCombatStart))]
public sealed class FakeSneckoEyeStats : SimpleCounterStats<FakeSneckoEye>
{
    public override string Format => "Applied [gold]Confused[/gold] {0} times.";
    public static void Postfix(FakeSneckoEye __instance) =>
        Track(__instance, s => s.Amount++);
}

// FencingManual: gains Forge on turn 1
[HarmonyPatch(typeof(FencingManual), nameof(FencingManual.AfterSideTurnStart))]
public sealed class FencingManualStats : SimpleCounterStats<FencingManual>
{
    public override string Format => "Gained {0} [gold]Forge[/gold].";
    public static void Postfix(FencingManual __instance, CombatSide side, CombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (combatState.RoundNumber > 1) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Forge.IntValue);
    }
}

// FuneraryMask: generates Soul cards on turn 1
[HarmonyPatch(typeof(FuneraryMask), nameof(FuneraryMask.AfterSideTurnStart))]
public sealed class FuneraryMaskStats : SimpleCounterStats<FuneraryMask>
{
    public override string Format => "Generated {0} Soul cards.";
    public static void Postfix(FuneraryMask __instance, CombatSide side, CombatState combatState)
    {
        if (side != CombatSide.Player) return;
        if (combatState.RoundNumber > 1) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Cards.IntValue);
    }
}

// GamePiece: draws cards when Powers are played
[HarmonyPatch(typeof(GamePiece), nameof(GamePiece.AfterCardPlayed))]
public sealed class GamePieceStats : SimpleCounterStats<GamePiece>
{
    public override string Format => "Drew {0} cards from Powers.";
    public static void Postfix(GamePiece __instance, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != __instance.Owner) return;
        if (cardPlay.Card.Type != CardType.Power) return;
        if (!CombatManager.Instance.IsInProgress) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Cards.IntValue);
    }
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
}

// HandDrill: applies Vulnerable when block is broken
[HarmonyPatch(typeof(HandDrill), nameof(HandDrill.AfterDamageGiven))]
public sealed class HandDrillStats : SimpleCounterStats<HandDrill>
{
    public override string Format => "Applied [gold]Vulnerable[/gold] {0} times.";
    public static void Postfix(HandDrill __instance, Creature? dealer, DamageResult result)
    {
        if (dealer != __instance.Owner.Creature && dealer?.PetOwner != __instance.Owner) return;
        if (!result.WasBlockBroken) return;
        Track(__instance, s => s.Amount++);
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
}

// MusicBox: copies first attack each turn as Ethereal
[HarmonyPatch(typeof(MusicBox), nameof(MusicBox.AfterCardPlayed))]
public sealed class MusicBoxStats : SimpleCounterStats<MusicBox>
{
    public override string Format => "Copied {0} attacks as [gold]Ethereal[/gold].";
    private static readonly System.Reflection.FieldInfo _cardBeingPlayedField =
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
}

// OddlySmoothStone: applies Dexterity at combat start
[HarmonyPatch(typeof(OddlySmoothStone), nameof(OddlySmoothStone.AfterRoomEntered))]
public sealed class OddlySmoothStoneStats : SimpleCounterStats<OddlySmoothStone>
{
    public override string Format => "Applied {0} [gold]Dexterity[/gold].";
    public static void Postfix(OddlySmoothStone __instance, AbstractRoom room)
    {
        if (room is not CombatRoom) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Dexterity.IntValue);
    }
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
}

// RunicCapacitor: adds orb slots on turn 1
[HarmonyPatch(typeof(RunicCapacitor), nameof(RunicCapacitor.AfterSideTurnStart))]
public sealed class RunicCapacitorStats : SimpleCounterStats<RunicCapacitor>
{
    public override string Format => "Added {0} orb slots.";
    public static void Postfix(RunicCapacitor __instance, CombatSide side, CombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (combatState.RoundNumber > 1) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Repeat.IntValue);
    }
}

// SparklingRouge: gains Strength and Dexterity on block clear in round 3
[HarmonyPatch(typeof(SparklingRouge), nameof(SparklingRouge.AfterBlockCleared))]
public sealed class SparklingRougeStats : SimpleCounterStats<SparklingRouge>
{
    public override string Format => "Gained [gold]Strength[/gold]+[gold]Dexterity[/gold] {0} times.";
    public static void Postfix(SparklingRouge __instance, Creature creature)
    {
        if (creature != __instance.Owner.Creature) return;
        if (creature.CombatState.RoundNumber != 3) return;
        Track(__instance, s => s.Amount++);
    }
}

// StoneCracker: upgrades cards in boss combats
[HarmonyPatch(typeof(StoneCracker), nameof(StoneCracker.AfterRoomEntered))]
public sealed class StoneCrackerStats : SimpleCounterStats<StoneCracker>
{
    public override string Format => "Upgraded {0} cards in boss combats.";
    public static void Postfix(StoneCracker __instance, AbstractRoom room)
    {
        if (room.RoomType != RoomType.Boss) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Cards.IntValue);
    }
}

// SwordOfJade: applies Strength at combat start
[HarmonyPatch(typeof(SwordOfJade), nameof(SwordOfJade.AfterRoomEntered))]
public sealed class SwordOfJadeStats : SimpleCounterStats<SwordOfJade>
{
    public override string Format => "Applied {0} [gold]Strength[/gold].";
    public static void Postfix(SwordOfJade __instance, AbstractRoom room)
    {
        if (room is not CombatRoom) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Strength.IntValue);
    }
}

// SwordOfStone: tracks elites defeated toward transformation
[HarmonyPatch(typeof(SwordOfStone), nameof(SwordOfStone.AfterCombatVictory))]
public sealed class SwordOfStoneStats : SimpleCounterStats<SwordOfStone>
{
    public override string Format => "Defeated {0} elites.";
    public static void Postfix(SwordOfStone __instance, CombatRoom room)
    {
        if (room.RoomType != RoomType.Elite) return;
        Track(__instance, s => s.Amount++);
    }
}

// SymbioticVirus: channels Dark orbs on turn 1
[HarmonyPatch(typeof(SymbioticVirus), nameof(SymbioticVirus.AfterSideTurnStart))]
public sealed class SymbioticVirusStats : SimpleCounterStats<SymbioticVirus>
{
    public override string Format => "Channeled {0} [gold]Dark[/gold] orbs.";
    public static void Postfix(SymbioticVirus __instance, CombatSide side, CombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (combatState.RoundNumber > 1) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars["Dark"].IntValue);
    }
}

// ToastyMittens: gains Strength and exhausts a card each turn
[HarmonyPatch(typeof(ToastyMittens), nameof(ToastyMittens.BeforeHandDraw))]
public sealed class ToastyMittensStats : SimpleCounterStats<ToastyMittens>
{
    public override string Format => "Gained {0} [gold]Strength[/gold] and exhausted cards.";
    public static void Postfix(ToastyMittens __instance, Player player)
    {
        if (player != __instance.Owner.Creature.Player) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Strength.IntValue);
    }
}

// WarHammer: upgrades cards after elite victories
[HarmonyPatch(typeof(WarHammer), nameof(WarHammer.AfterCombatVictory))]
public sealed class WarHammerStats : SimpleCounterStats<WarHammer>
{
    public override string Format => "Upgraded {0} cards after elite combats.";
    public static void Postfix(WarHammer __instance, CombatRoom room)
    {
        if (room.RoomType != RoomType.Elite) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Cards.IntValue);
    }
}

// WongosMysteryTicket: tracks combats toward relic reward
[HarmonyPatch(typeof(WongosMysteryTicket), nameof(WongosMysteryTicket.AfterCombatEnd))]
public sealed class WongosMysteryTicketStats : SimpleCounterStats<WongosMysteryTicket>
{
    public override string Format => "Completed {0} combats toward relic.";
    public static void Postfix(WongosMysteryTicket __instance) =>
        Track(__instance, s => s.Amount++);
}

// BigHat: generates Ethereal cards on turn 1
[HarmonyPatch(typeof(BigHat), nameof(BigHat.AfterSideTurnStart))]
public sealed class BigHatStats : SimpleCounterStats<BigHat>
{
    public override string Format => "Generated {0} [gold]Ethereal[/gold] cards.";
    public static void Postfix(BigHat __instance, CombatSide side, CombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (combatState.RoundNumber > 1) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Cards.IntValue);
    }
}

// BingBong: duplicates cards added to deck
[HarmonyPatch(typeof(BingBong), nameof(BingBong.AfterCardChangedPiles))]
public sealed class BingBongStats : SimpleCounterStats<BingBong>
{
    public override string Format => "Duplicated {0} cards.";
    private static readonly System.Reflection.FieldInfo _cardsToSkipField =
        AccessTools.Field(typeof(BingBong), "_cardsToSkip");
    private static bool _willDuplicate;

    public static void Prefix(BingBong __instance, CardModel card, AbstractModel? source)
    {
        _willDuplicate = false;
        CardPile? pile = card.Pile;
        if (pile == null || pile.Type != PileType.Deck) return;
        if (card.Owner != __instance.Owner) return;
        if (source != null) return;
        var skip = (System.Collections.Generic.HashSet<CardModel>?)_cardsToSkipField.GetValue(__instance);
        if (skip != null && skip.Contains(card)) return;
        _willDuplicate = true;
    }

    public static void Postfix(BingBong __instance)
    {
        if (!_willDuplicate) return;
        Track(__instance, s => s.Amount++);
    }
}

// BoneTea: upgrades hand on turn 1 (limited uses)
[HarmonyPatch(typeof(BoneTea), nameof(BoneTea.AfterSideTurnStart))]
public sealed class BoneTeaStats : SimpleCounterStats<BoneTea>
{
    public override string Format => "Upgraded {0} hands.";
    private static bool _willUpgrade;

    public static void Prefix(BoneTea __instance, CombatSide side, CombatState combatState)
    {
        _willUpgrade = !__instance.IsUsedUp
            && side == __instance.Owner.Creature.Side
            && combatState.RoundNumber <= 1;
    }

    public static void Postfix(BoneTea __instance)
    {
        if (!_willUpgrade) return;
        Track(__instance, s => s.Amount++);
    }
}

// VexingPuzzlebox: generates a free card on turn 1
[HarmonyPatch(typeof(VexingPuzzlebox), nameof(VexingPuzzlebox.AfterPlayerTurnStart))]
public sealed class VexingPuzzleboxStats : SimpleCounterStats<VexingPuzzlebox>
{
    public override string Format => "Generated {0} free cards.";
    public static void Postfix(VexingPuzzlebox __instance, Player player)
    {
        if (player != __instance.Owner) return;
        if (__instance.Owner.Creature.CombatState.RoundNumber != 1) return;
        Track(__instance, s => s.Amount++);
    }
}

// ChoicesParadox: generates cards to choose from on turn 1
[HarmonyPatch(typeof(ChoicesParadox), nameof(ChoicesParadox.AfterPlayerTurnStart))]
public sealed class ChoicesParadoxStats : SimpleCounterStats<ChoicesParadox>
{
    public override string Format => "Generated {0} cards to choose from.";
    public static void Postfix(ChoicesParadox __instance, Player player)
    {
        if (player != __instance.Owner) return;
        if (__instance.Owner.Creature.CombatState.RoundNumber != 1) return;
        Track(__instance, s => s.Amount++);
    }
}

// JeweledMask: draws a free Power on turn 1
[HarmonyPatch(typeof(JeweledMask), nameof(JeweledMask.BeforeHandDraw))]
public sealed class JeweledMaskStats : SimpleCounterStats<JeweledMask>
{
    public override string Format => "Drew {0} free Powers.";
    public static void Postfix(JeweledMask __instance, Player player, CombatState combatState)
    {
        if (player != __instance.Owner) return;
        if (combatState.RoundNumber > 1) return;
        Track(__instance, s => s.Amount++);
    }
}

// VelvetChoker: tracks times card limit was hit
[HarmonyPatch(typeof(VelvetChoker), nameof(VelvetChoker.AfterCardPlayed))]
public sealed class VelvetChokerStats : SimpleCounterStats<VelvetChoker>
{
    public override string Format => "Hit card limit {0} times.";
    private static readonly System.Reflection.FieldInfo _cardsPlayedField =
        AccessTools.Field(typeof(VelvetChoker), "_cardsPlayedThisTurn");

    public static void Postfix(VelvetChoker __instance, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != __instance.Owner) return;
        int cardsPlayed = (int)_cardsPlayedField.GetValue(__instance)!;
        if (cardsPlayed != __instance.DynamicVars.Cards.IntValue) return;
        Track(__instance, s => s.Amount++);
    }
}

// DiamondDiadem: applies DiamondDiademPower when few cards played
[HarmonyPatch(typeof(DiamondDiadem), nameof(DiamondDiadem.BeforeTurnEnd))]
public sealed class DiamondDiademStats : SimpleCounterStats<DiamondDiadem>
{
    public override string Format => "Applied [gold]DiamondDiademPower[/gold] {0} times.";
    private static readonly System.Reflection.FieldInfo _cardsPlayedField =
        AccessTools.Field(typeof(DiamondDiadem), "_cardsPlayedThisTurn");

    public static void Postfix(DiamondDiadem __instance, CombatSide side)
    {
        if (side != __instance.Owner.Creature.Side) return;
        int cardsPlayed = (int)_cardsPlayedField.GetValue(__instance)!;
        if ((decimal)cardsPlayed > __instance.DynamicVars["CardThreshold"].BaseValue) return;
        Track(__instance, s => s.Amount++);
    }
}

// BeltBuckle: grants Dexterity when no potions held
[HarmonyPatch(typeof(BeltBuckle), nameof(BeltBuckle.BeforeCombatStart))]
public sealed class BeltBuckleStats : SimpleCounterStats<BeltBuckle>
{
    public override string Format => "Granted {0} [gold]Dexterity[/gold].";
    public static void Postfix(BeltBuckle __instance)
    {
        if (__instance.Owner.Potions.Any()) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Dexterity.IntValue);
    }
}
