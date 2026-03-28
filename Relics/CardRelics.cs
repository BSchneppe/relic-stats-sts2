using System;
using System.Globalization;
using System.Text.Json.Nodes;
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

namespace RelicStats.Relics;

// ── Simple draw modifiers ──────────────────────────────────────────────

// BagOfPreparation: +2 cards drawn turn 1
[HarmonyPatch(typeof(BagOfPreparation), nameof(BagOfPreparation.ModifyHandDraw))]
public sealed class BagOfPreparationStats : SimpleCounterStats<BagOfPreparation>
{
    public override string Format => "Drew {0} additional cards.";
    public static void Postfix(BagOfPreparation __instance, Player player, decimal __result, decimal __1)
    {
        if (__result <= __1) return;
        Track(__instance, s => s.Amount += (int)(__result - __1));
    }
}

// BigMushroom: -2 cards drawn turn 1 (track as positive number of cards lost)
[HarmonyPatch(typeof(BigMushroom), nameof(BigMushroom.ModifyHandDraw))]
public sealed class BigMushroomStats : SimpleCounterStats<BigMushroom>
{
    public override string Format => "Drew {0} fewer cards.";
    public static void Postfix(BigMushroom __instance, Player player, decimal __result, decimal __1)
    {
        if (__result >= __1) return;
        Track(__instance, s => s.Amount += (int)(__1 - __result));
    }
}

// BoomingConch: +2 cards drawn vs elites turn 1
[HarmonyPatch(typeof(BoomingConch), nameof(BoomingConch.ModifyHandDraw))]
public sealed class BoomingConchStats : SimpleCounterStats<BoomingConch>
{
    public override string Format => "Drew {0} additional cards.";
    public static void Postfix(BoomingConch __instance, Player player, decimal __result, decimal __1)
    {
        if (__result <= __1) return;
        Track(__instance, s => s.Amount += (int)(__result - __1));
    }
}

// Fiddle: +2 cards drawn (late modifier, every turn)
[HarmonyPatch(typeof(Fiddle), nameof(Fiddle.ModifyHandDrawLate))]
public sealed class FiddleStats : SimpleCounterStats<Fiddle>
{
    public override string Format => "Drew {0} additional cards.";
    public static void Postfix(Fiddle __instance, Player player, decimal __result, decimal __1)
    {
        if (__result <= __1) return;
        Track(__instance, s => s.Amount += (int)(__result - __1));
    }
}

// PaelsBlood: +1 card drawn every turn
[HarmonyPatch(typeof(PaelsBlood), nameof(PaelsBlood.ModifyHandDraw))]
public sealed class PaelsBloodStats : SimpleCounterStats<PaelsBlood>
{
    public override string Format => "Drew {0} additional cards.";
    public static void Postfix(PaelsBlood __instance, Player player, decimal __result, decimal __1)
    {
        if (__result <= __1) return;
        Track(__instance, s => s.Amount += (int)(__result - __1));
    }
}

// RingOfTheDrake: +2 cards drawn first N turns
[HarmonyPatch(typeof(RingOfTheDrake), nameof(RingOfTheDrake.ModifyHandDraw))]
public sealed class RingOfTheDrakeStats : SimpleCounterStats<RingOfTheDrake>
{
    public override string Format => "Drew {0} additional cards.";
    public static void Postfix(RingOfTheDrake __instance, Player player, decimal __result, decimal __1)
    {
        if (__result <= __1) return;
        Track(__instance, s => s.Amount += (int)(__result - __1));
    }
}

// RingOfTheSnake: +2 cards drawn turn 1
[HarmonyPatch(typeof(RingOfTheSnake), nameof(RingOfTheSnake.ModifyHandDraw))]
public sealed class RingOfTheSnakeStats : SimpleCounterStats<RingOfTheSnake>
{
    public override string Format => "Drew {0} additional cards.";
    public static void Postfix(RingOfTheSnake __instance, Player player, decimal __result, decimal __1)
    {
        if (__result <= __1) return;
        Track(__instance, s => s.Amount += (int)(__result - __1));
    }
}

// ── Retention relics ───────────────────────────────────────────────────

// RingingTriangle: retains hand turn 1 only
[HarmonyPatch(typeof(RingingTriangle), nameof(RingingTriangle.ShouldFlush))]
public sealed class RingingTriangleStats : SimpleCounterStats<RingingTriangle>
{
    public override string Format => "Retained hand {0} times.";
    public static void Postfix(RingingTriangle __instance, Player player, bool __result)
    {
        // ShouldFlush returns false when retaining
        if (__result) return;
        if (player != __instance.Owner) return;
        Track(__instance, s => s.Amount++);
    }
}

// RunicPyramid: retains hand every turn
[HarmonyPatch(typeof(RunicPyramid), nameof(RunicPyramid.ShouldFlush))]
public sealed class RunicPyramidStats : SimpleCounterStats<RunicPyramid>
{
    public override string Format => "Retained hand {0} times.";
    public static void Postfix(RunicPyramid __instance, Player player, bool __result)
    {
        if (__result) return;
        if (player != __instance.Owner) return;
        Track(__instance, s => s.Amount++);
    }
}

// ── Card generation relics ─────────────────────────────────────────────

// OrangeDough: adds colorless cards to hand turn 1
[HarmonyPatch(typeof(OrangeDough), nameof(OrangeDough.AfterSideTurnStart))]
public sealed class OrangeDoughStats : SimpleCounterStats<OrangeDough>
{
    public override string Format => "Added {0} colorless cards.";
    public static void Postfix(OrangeDough __instance, CombatSide side, CombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (combatState.RoundNumber > 1) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Cards.IntValue);
    }
}

// RadiantPearl: generates Luminesce cards turn 1
[HarmonyPatch(typeof(RadiantPearl), nameof(RadiantPearl.BeforeHandDraw))]
public sealed class RadiantPearlStats : SimpleCounterStats<RadiantPearl>
{
    public override string Format => "Generated {0} Luminesce cards.";
    public static void Postfix(RadiantPearl __instance, Player player, CombatState combatState)
    {
        if (player != __instance.Owner) return;
        if (combatState.RoundNumber != 1) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Cards.IntValue);
    }
}

// NinjaScroll: creates Shivs in hand turn 1
[HarmonyPatch(typeof(NinjaScroll), nameof(NinjaScroll.BeforeHandDraw))]
public sealed class NinjaScrollStats : SimpleCounterStats<NinjaScroll>
{
    public override string Format => "Created {0} Shivs.";
    public static void Postfix(NinjaScroll __instance, Player player, CombatState combatState)
    {
        if (player != __instance.Owner) return;
        if (combatState.RoundNumber > 1) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars["Shivs"].IntValue);
    }
}

// ── Draw-on-trigger relics ─────────────────────────────────────────────

// CentennialPuzzle: draws cards on first unblocked hit per combat
// We use a Prefix to capture UsedThisCombat before the original method sets it to true.
[HarmonyPatch(typeof(CentennialPuzzle), nameof(CentennialPuzzle.AfterDamageReceived))]
public sealed class CentennialPuzzleStats : SimpleCounterStats<CentennialPuzzle>
{
    public override string Format => "Drew {0} cards on hit.";

    [ThreadStatic] private static bool _wasUsed;

    public static void Prefix(CentennialPuzzle __instance)
    {
        _wasUsed = __instance.UsedThisCombat;
    }

    public static void Postfix(CentennialPuzzle __instance, Creature target,
        DamageResult result)
    {
        if (target != __instance.Owner.Creature) return;
        if (result.UnblockedDamage <= 0) return;
        if (_wasUsed) return;
        if (!CombatManager.Instance.IsInProgress) return;
        Track(__instance, s => s.Amount += (int)__instance.DynamicVars.Cards.BaseValue);
    }
}

// UnceasingTop: draws a card when hand empties
[HarmonyPatch(typeof(UnceasingTop), nameof(UnceasingTop.AfterHandEmptied))]
public sealed class UnceasingTopStats : SimpleCounterStats<UnceasingTop>
{
    public override string Format => "Drew {0} cards from empty hand.";
    public static void Postfix(UnceasingTop __instance, Player player)
    {
        if (!CombatManager.Instance.IsPlayPhase) return;
        if (player != __instance.Owner) return;
        Track(__instance, s => s.Amount++);
    }
}

// GamblingChip: discards and redraws cards turn 1
[HarmonyPatch(typeof(GamblingChip), nameof(GamblingChip.AfterPlayerTurnStart))]
public sealed class GamblingChipStats : SimpleCounterStats<GamblingChip>
{
    public override string Format => "Swapped {0} cards.";
    // GamblingChip is async and the discard count is determined at runtime by player choice.
    // We patch AfterPlayerTurnStart; the actual swap count is internal. We track that
    // the effect triggered (once per combat, turn 1). Precise swap count would require
    // deeper hooking into CardCmd.DiscardAndDraw which is beyond simple counter scope.
    public static void Postfix(GamblingChip __instance, Player player)
    {
        if (player != __instance.Owner) return;
        if (__instance.Owner.Creature.CombatState.RoundNumber > 1) return;
        Track(__instance, s => s.Amount++);
    }
}

// Bookmark: reduces cost of a retained card at turn end
[HarmonyPatch(typeof(Bookmark), nameof(Bookmark.AfterTurnEnd))]
public sealed class BookmarkStats : SimpleCounterStats<Bookmark>
{
    public override string Format => "Reduced card costs {0} times.";
    public static void Postfix(Bookmark __instance, CombatSide side)
    {
        if (side != __instance.Owner.Creature.Side) return;
        // The original only acts when there's at least one retained card with cost > 0.
        // We replicate the check to avoid false positives.
        var hand = PileType.Hand.GetPile(__instance.Owner).Cards;
        bool anyEligible = false;
        foreach (var c in hand)
        {
            if (c.ShouldRetainThisTurn && c.EnergyCost.GetWithModifiers(CostModifiers.Local) > 0 && !c.EnergyCost.CostsX)
            {
                anyEligible = true;
                break;
            }
        }
        if (!anyEligible) return;
        Track(__instance, s => s.Amount++);
    }
}

// ── Conditional draw relics ────────────────────────────────────────────

// Pocketwatch: +3 cards if <= threshold cards played last turn
[HarmonyPatch(typeof(Pocketwatch), nameof(Pocketwatch.ModifyHandDraw))]
public sealed class PocketwatchStats : SimpleCounterStats<Pocketwatch>
{
    public override string Format => "Drew {0} additional cards.";
    public static void Postfix(Pocketwatch __instance, Player player, decimal __result, decimal __1)
    {
        if (__result <= __1) return;
        Track(__instance, s => s.Amount += (int)(__result - __1));
    }
}

// PollinousCore: +2 cards every Nth turn
[HarmonyPatch(typeof(PollinousCore), nameof(PollinousCore.ModifyHandDraw))]
public sealed class PollinousCoreStats : SimpleCounterStats<PollinousCore>
{
    public override string Format => "Drew {0} additional cards.";
    public static void Postfix(PollinousCore __instance, Player player, decimal __result, decimal __1)
    {
        if (__result <= __1) return;
        Track(__instance, s => s.Amount += (int)(__result - __1));
    }
}

// ── SneckoEye: complex multi-value tracking ────────────────────────────

public sealed class SneckoEyeStats : IRelicStats
{
    public string RelicId => RelicIdHelper.Slugify(nameof(SneckoEye));

    public int CardsDrawn { get; set; }
    public int Cost0 { get; set; }
    public int Cost1 { get; set; }
    public int Cost2 { get; set; }
    public int Cost3 { get; set; }
    public int TotalDiscount { get; set; }
    public int TurnWhenObtained { get; set; }
    public int CombatWhenObtained { get; set; }
    public int? FrozenTurnCount { get; set; }
    public int? FrozenCombatCount { get; set; }

    public string GetDescription(int totalTurns, int totalCombats)
    {
        var totalCards = Cost0 + Cost1 + Cost2 + Cost3;
        var avgDiscount = totalCards > 0
            ? ((float)TotalDiscount / totalCards).ToString("0.##", CultureInfo.InvariantCulture)
            : "0";

        return $"Drew {Fmt.Blue(CardsDrawn)} additional cards.\n" +
               $"Card cost counts:\n" +
               $"  0 energy: {Fmt.Blue(Cost0)}  1 energy: {Fmt.Blue(Cost1)}\n" +
               $"  2 energy: {Fmt.Blue(Cost2)}  3 energy: {Fmt.Blue(Cost3)}\n" +
               $"Average discount: {Fmt.Blue(avgDiscount)}";
    }

    public JsonObject Save()
    {
        var obj = new JsonObject
        {
            ["cardsDrawn"] = CardsDrawn,
            ["cost0"] = Cost0, ["cost1"] = Cost1,
            ["cost2"] = Cost2, ["cost3"] = Cost3,
            ["totalDiscount"] = TotalDiscount,
            ["turnObtained"] = TurnWhenObtained,
            ["combatObtained"] = CombatWhenObtained,
        };
        if (FrozenTurnCount.HasValue) obj["frozenTurns"] = FrozenTurnCount.Value;
        if (FrozenCombatCount.HasValue) obj["frozenCombats"] = FrozenCombatCount.Value;
        return obj;
    }

    public void Load(JsonObject data)
    {
        CardsDrawn = data["cardsDrawn"]?.GetValue<int>() ?? 0;
        Cost0 = data["cost0"]?.GetValue<int>() ?? 0;
        Cost1 = data["cost1"]?.GetValue<int>() ?? 0;
        Cost2 = data["cost2"]?.GetValue<int>() ?? 0;
        Cost3 = data["cost3"]?.GetValue<int>() ?? 0;
        TotalDiscount = data["totalDiscount"]?.GetValue<int>() ?? 0;
        TurnWhenObtained = data["turnObtained"]?.GetValue<int>() ?? 0;
        CombatWhenObtained = data["combatObtained"]?.GetValue<int>() ?? 0;
        FrozenTurnCount = data["frozenTurns"]?.GetValue<int>();
        FrozenCombatCount = data["frozenCombats"]?.GetValue<int>();
    }

    public void Reset()
    {
        CardsDrawn = 0;
        Cost0 = Cost1 = Cost2 = Cost3 = 0;
        TotalDiscount = 0;
        TurnWhenObtained = RelicStatsRegistry.TurnCount;
        CombatWhenObtained = RelicStatsRegistry.CombatCount;
        FrozenTurnCount = null;
        FrozenCombatCount = null;
    }

    public static SneckoEyeStats? GetFor(SneckoEye instance)
    {
        if (instance.IsMelted) return null;
        if (!LocalContext.IsMine(instance)) return null;
        return RelicStatsRegistry.Get(RelicIdHelper.Slugify(nameof(SneckoEye))) as SneckoEyeStats;
    }
}

// SneckoEye draw patch: +2 cards drawn every turn
[HarmonyPatch(typeof(SneckoEye), nameof(SneckoEye.ModifyHandDraw))]
public static class SneckoEyeDrawPatch
{
    public static void Postfix(SneckoEye __instance, Player player, decimal __result, decimal __1)
    {
        if (__result <= __1) return;
        var stats = SneckoEyeStats.GetFor(__instance);
        if (stats == null) return;
        stats.CardsDrawn += (int)(__result - __1);
    }
}

// SneckoEye confusion cost patch: track cost changes from Confused power
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Models.Powers.ConfusedPower),
    nameof(MegaCrit.Sts2.Core.Models.Powers.ConfusedPower.AfterCardDrawn))]
public static class SneckoEyeConfusionPatch
{
    public static void Postfix(MegaCrit.Sts2.Core.Models.Powers.ConfusedPower __instance,
        CardModel card, bool fromHandDraw)
    {
        if (card.Owner == null) return;

        // Find SneckoEye on the card owner -- Confused is applied by SneckoEye
        var sneckoEye = card.Owner.GetRelic<SneckoEye>();
        if (sneckoEye == null) return;

        var stats = SneckoEyeStats.GetFor(sneckoEye);
        if (stats == null) return;

        int originalCost = card.EnergyCost.Canonical;
        if (originalCost < 0) return;

        int newCost = card.EnergyCost.GetWithModifiers(CostModifiers.Local);
        if (newCost < 0) return;

        // Track per-cost-tier
        switch (newCost)
        {
            case 0: stats.Cost0++; break;
            case 1: stats.Cost1++; break;
            case 2: stats.Cost2++; break;
            default: stats.Cost3++; break;
        }

        // Track discount (positive = saved energy, negative = cost more)
        stats.TotalDiscount += originalCost - newCost;
    }
}
