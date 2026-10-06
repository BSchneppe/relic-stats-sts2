using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;
using RelicStats.Patches;
using RelicStats.Core;
#if DEBUG
using RelicStats.Core.Testing;
#endif

namespace RelicStats.Relics;

// ── Simple draw modifiers ──────────────────────────────────────────────

// BagOfPreparation: +2 cards drawn turn 1
[HarmonyPatch(typeof(BagOfPreparation), nameof(BagOfPreparation.ModifyHandDraw))]
public sealed class BagOfPreparationStats : SimpleCounterStats<BagOfPreparation>
{
    public override string Format => "Requested {0} extra cards from turn-start draw.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(BagOfPreparation __instance, Player player, decimal __result, decimal __1)
    {
        if (__result <= __1) return;
        Track(__instance, s => s.Amount += (int)(__result - __1));
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked extra draw", () =>
            new TestResult(Amount == 2, $"expected 2, got {Amount}"));
        runner.Do("end turn", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("no increment on turn 2", () =>
            new TestResult(Amount == 2, $"expected still 2, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// BigMushroom: -2 cards drawn turn 1 (track as positive number of cards lost)
[HarmonyPatch(typeof(BigMushroom), nameof(BigMushroom.ModifyHandDraw))]
public sealed class BigMushroomStats : SimpleCounterStats<BigMushroom>
{
    public override string Format => "Removed {0} cards from the turn-start draw request.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(BigMushroom __instance, Player player, decimal __result, decimal __1)
    {
        if (__result >= __1) return;
        Track(__instance, s => s.Amount += (int)(__1 - __result));
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked fewer cards drawn", () =>
            new TestResult(Amount == 2, $"expected 2, got {Amount}"));
        runner.Do("end turn", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("no increment on turn 2", () =>
            new TestResult(Amount == 2, $"expected still 2, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// BoomingConch: +2 cards drawn vs elites turn 1
[HarmonyPatch(typeof(BoomingConch), nameof(BoomingConch.ModifyHandDraw))]
public sealed class BoomingConchStats : SimpleCounterStats<BoomingConch>
{
    public override string Format => "Requested {0} extra cards from turn-start draw.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(BoomingConch __instance, Player player, decimal __result, decimal __1)
    {
        if (__result <= __1) return;
        Track(__instance, s => s.Amount += (int)(__result - __1));
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start elite fight", () => TestHelpers.StartFight("BYGONE_EFFIGY_ELITE"));
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked extra draw vs elite", () =>
            new TestResult(Amount == 2, $"expected 2, got {Amount}"));
        runner.Do("end turn", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("no increment on turn 2", () =>
            new TestResult(Amount == 2, $"expected still 2, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// Fiddle: +2 cards drawn every turn
[HarmonyPatch]
public sealed class FiddleStats : SimpleCounterStats<Fiddle>
{
    public override string Format => "Requested {0} extra cards from turn-start draw.";

    // Renamed in 0.110; both take (Player, decimal count), so one postfix covers either.
    public static IEnumerable<MethodBase> TargetMethods() =>
        PatchTarget.FirstDeclared(typeof(Fiddle),
            nameof(Fiddle.ModifyHandDraw), nameof(Fiddle.ModifyHandDrawLate));

    public static void Postfix(Fiddle __instance, decimal __result, decimal __1)
    {
        if (__result <= __1) return;
        Track(__instance, s => s.Amount += (int)(__result - __1));
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked cards turn 1", () =>
            new TestResult(Amount == 2, $"expected 2, got {Amount}"));
        runner.Do("end turn", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked cards turn 2", () =>
            new TestResult(Amount == 4, $"expected 4, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// PaelsBlood: +1 card drawn every turn
[HarmonyPatch(typeof(PaelsBlood), nameof(PaelsBlood.ModifyHandDraw))]
public sealed class PaelsBloodStats : SimpleCounterStats<PaelsBlood>
{
    public override string Format => "Requested {0} extra cards from turn-start draw.";
    public static void Postfix(PaelsBlood __instance, Player player, decimal __result, decimal __1)
    {
        if (__result <= __1) return;
        Track(__instance, s => s.Amount += (int)(__result - __1));
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked cards turn 1", () =>
            new TestResult(Amount == 1, $"expected 1, got {Amount}"));
        runner.Do("end turn", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked cards turn 2", () =>
            new TestResult(Amount == 2, $"expected 2, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// RingOfTheDrake: +2 cards drawn first N turns (base: 3 turns)
[HarmonyPatch(typeof(RingOfTheDrake), nameof(RingOfTheDrake.ModifyHandDraw))]
public sealed class RingOfTheDrakeStats : SimpleCounterStats<RingOfTheDrake>
{
    public override string Format => "Requested {0} extra cards from turn-start draw.";
    public override StatCadence Cadence => StatCadence.Combat;
    public static void Postfix(RingOfTheDrake __instance, Player player, decimal __result, decimal __1)
    {
        if (__result <= __1) return;
        Track(__instance, s => s.Amount += (int)(__result - __1));
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked cards turn 1", () =>
            new TestResult(Amount == 2, $"expected 2, got {Amount}"));
        runner.Do("end turn", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked cards turn 2", () =>
            new TestResult(Amount == 4, $"expected 4, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// RingOfTheSnake: +2 cards drawn turn 1
[HarmonyPatch(typeof(RingOfTheSnake), nameof(RingOfTheSnake.ModifyHandDraw))]
public sealed class RingOfTheSnakeStats : SimpleCounterStats<RingOfTheSnake>
{
    public override string Format => "Requested {0} extra cards from turn-start draw.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(RingOfTheSnake __instance, Player player, decimal __result, decimal __1)
    {
        if (__result <= __1) return;
        Track(__instance, s => s.Amount += (int)(__result - __1));
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked cards", () =>
            new TestResult(Amount == 2, $"expected 2, got {Amount}"));
        runner.Do("end turn", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("no increment on turn 2", () =>
            new TestResult(Amount == 2, $"expected still 2, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// ── Retention relics ───────────────────────────────────────────────────

public abstract class RetainedCardStats<TRelic> : SimpleCounterStats<TRelic> where TRelic : RelicModel
{
    public int CardsRetained { get; set; }
    private bool _changedDuringRun;
    public override string Format => "Preserved {0} otherwise-discardable cards.";
    public override string GetDescription(int turns, int combats)
    {
        var text = string.Format(Format, Fmt.Blue(CardsRetained));
        if (_changedDuringRun)
            return text + "\nMeasured since update.\nEarlier hand-flush vetoes: " + Fmt.Blue(Amount) + ".";
        if (Cadence == StatCadence.Turn)
            text += "\nPer turn: " + Fmt.Blue(((float)CardsRetained / Math.Max(turns, 1)).ToString("0.###", CultureInfo.InvariantCulture));
        if (Cadence != StatCadence.Total)
            text += "\nPer combat: " + Fmt.Blue(((float)CardsRetained / Math.Max(combats, 1)).ToString("0.###", CultureInfo.InvariantCulture));
        return text;
    }
    public override JsonObject Save() { var data = base.Save(); data["retainedCards"] = CardsRetained; data["changedDuringRun"] = _changedDuringRun; return data; }
    public override void Load(JsonObject data) { base.Load(data); CardsRetained = data["retainedCards"]?.GetValue<int>() ?? 0; _changedDuringRun = data["changedDuringRun"]?.GetValue<bool>() ?? data["retainedCards"] == null; }
    public override void Reset() { base.Reset(); CardsRetained = 0; _changedDuringRun = false; }
    internal static void CountRetained(TRelic relic) => Track(relic, stats =>
    {
        ((RetainedCardStats<TRelic>)stats).CardsRetained +=
            PileType.Hand.GetPile(relic.Owner).Cards.Count(card => !card.ShouldRetainThisTurn);
    });
}

// RingingTriangle: retains hand turn 1 only
[HarmonyPatch(typeof(RingingTriangle), nameof(RingingTriangle.ShouldFlush))]
public sealed class RingingTriangleStats : RetainedCardStats<RingingTriangle>
{
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(RingingTriangle __instance, Player player, bool __result)
    {
        // ShouldFlush returns false when retaining
        if (__result) return;
        if (player != __instance.Owner) return;
        CountRetained(__instance);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("spawn cards and end turn 1", () =>
        {
            TestHelpers.EnableGodMode();
            TestHelpers.ProtectEnemy();
            TestHelpers.DiscardHand();
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.SpawnCard("DEFEND");
            TestHelpers.EndTurn();
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("retained the turn-1 hand once", () =>
            new TestResult(CardsRetained == 2, $"expected 2, got {CardsRetained}"));
        // ShouldFlush only returns false on turn 1; the turn-2 flush must not count.
        runner.Do("end turn 2", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("no retention from turn 2 on", () =>
            new TestResult(CardsRetained == 2, $"expected still 2, got {CardsRetained}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// RunicPyramid: retains hand every turn
[HarmonyPatch(typeof(RunicPyramid), nameof(RunicPyramid.ShouldFlush))]
public sealed class RunicPyramidStats : RetainedCardStats<RunicPyramid>
{
    public static void Postfix(RunicPyramid __instance, Player player, bool __result)
    {
        if (__result) return;
        if (player != __instance.Owner) return;
        CountRetained(__instance);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Assert("legacy retention counts have no new measurement rates", () =>
        {
            var migrated = new RunicPyramidStats();
            migrated.Load(new JsonObject { ["amount"] = 8 });
            migrated.CardsRetained = 3;
            var restored = new RunicPyramidStats();
            restored.Load(migrated.Save());
            var text = restored.GetDescription(20, 10);
            return new TestResult(restored.Amount == 8 && restored.CardsRetained == 3 &&
                text.Contains("Measured since update") && text.Contains("Earlier hand-flush vetoes") &&
                !text.Contains("Per combat") && !text.Contains("Per turn"),
                "expected retained cards separated from old vetoes without historical denominators");
        });
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("nothing retained before the first flush", () =>
            new TestResult(CardsRetained == 0, $"expected 0, got {CardsRetained}"));
        runner.Do("spawn cards and end turn 1", () =>
        {
            TestHelpers.EnableGodMode();
            TestHelpers.ProtectEnemy();
            TestHelpers.DiscardHand();
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.SpawnCard("DEFEND");
            TestHelpers.EndTurn();
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("retained once after turn 1", () =>
            new TestResult(CardsRetained == 2, $"expected 2, got {CardsRetained}"));
        runner.Do("end turn 2", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("retained again after turn 2", () =>
            new TestResult(CardsRetained == 4, $"expected 4, got {CardsRetained}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// ── Card generation relics ─────────────────────────────────────────────

// OrangeDough: adds colorless cards to hand turn 1
[HarmonyPatch(typeof(OrangeDough), nameof(OrangeDough.AfterSideTurnStart))]
public sealed class OrangeDoughStats : SimpleCounterStats<OrangeDough>
{
    public override string Format => "Added {0} colorless cards.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(OrangeDough __instance, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!participants.Contains(__instance.Owner.Creature)) return;
        if (__instance.Owner.PlayerCombatState!.TurnNumber > 1) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Cards.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.SideTurnStart);
        runner.Assert("tracked cards", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Cards.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        // AfterSideTurnStart for the player's turn 2 runs after AfterPlayerTurnStart, so wait for
        // both: the enemy's SideTurnStart arrives (and is ignored) while waiting for PlayerTurnStart.
        runner.Do("end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.WaitFor(GameEvent.SideTurnStart, 15000);
        runner.Assert("no increment on turn 2", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Cards.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// RadiantPearl: generates Luminesce cards turn 1
[HarmonyPatch(typeof(RadiantPearl), nameof(RadiantPearl.BeforeHandDraw))]
public sealed class RadiantPearlStats : SimpleCounterStats<RadiantPearl>
{
    public override string Format => "Generated {0} Luminesce cards.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(RadiantPearl __instance, Player player, ICombatState combatState)
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
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked cards", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Cards.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Do("end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("no increment on turn 2", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Cards.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// NinjaScroll: creates Shivs in hand turn 1
[HarmonyPatch(typeof(NinjaScroll), nameof(NinjaScroll.BeforeHandDraw))]
public sealed class NinjaScrollStats : SimpleCounterStats<NinjaScroll>
{
    public override string Format => "Created {0} Shivs.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(NinjaScroll __instance, Player player, ICombatState combatState)
    {
        if (player != __instance.Owner) return;
        if (__instance.Owner.PlayerCombatState!.TurnNumber > 1) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars["Shivs"].IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked cards", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars["Shivs"].IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Do("end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("no increment on turn 2", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars["Shivs"].IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// ── Draw-on-trigger relics ─────────────────────────────────────────────

// CentennialPuzzle: draws cards on first unblocked hit per combat
// We use a Prefix to capture UsedThisCombat before the original method sets it to true.
[HarmonyPatch(typeof(CentennialPuzzle), nameof(CentennialPuzzle.AfterDamageReceived))]
public sealed class CentennialPuzzleStats : MeasuredCounterStats<CentennialPuzzle>
{
    protected override string PreviousMeasurement => "requested draws";
    public override string Format => "Drew {0} cards on hit.";
    public override StatCadence Cadence => StatCadence.Total;

    internal static void Prefix(CentennialPuzzle __instance, out DirectDrawScope __state) =>
        __state = DirectDrawScope.Begin(__instance.Owner,
            count => Track(__instance, s => s.Amount += count), firstOnly: false);

    internal static void Finalizer(DirectDrawScope __state) => __state.Dispose();

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Assert("old requests remain separate after save/load", () =>
        {
            var migrated = new CentennialPuzzleStats();
            migrated.Load(new JsonObject { ["amount"] = 9 });
            migrated.Amount = 2;
            var restored = new CentennialPuzzleStats();
            restored.Load(migrated.Save());
            return new TestResult(restored.Amount == 2 && restored.Save()["previousAmount"]?.GetValue<int>() == 9 &&
                restored.GetDescription(10, 3).Contains("Earlier requested draws"),
                "expected completed draw total 2 and preserved earlier request total 9");
        });
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("seed three cards and take an unblocked hit", () =>
        {
            TestHelpers.ProtectEnemy();
            for (int i = 0; i < 3; i++) TestHelpers.SpawnCard("STRIKE_IRONCLAD", "draw");
            TestHelpers.DealDamageToPlayer(5);
        });
        runner.WaitFor(GameEvent.DamageReceived);
        runner.WaitUntil("all three direct draws complete", () => Amount == 3);
        runner.Assert("three sequential draws each count", () => new TestResult(Amount == 3, $"expected 3, got {Amount}"));
        runner.Do("take a second hit", () => TestHelpers.DealDamageToPlayer(5));
        runner.WaitFor(GameEvent.DamageReceived);
        runner.Assert("puzzle is used once per combat", () => new TestResult(Amount == 3, $"expected 3, got {Amount}"));
        runner.Do("replace puzzle and prevent its draws with Fiddle", () =>
        {
            TestHelpers.RemoveRelic(RelicId);
            TestHelpers.AddRelic(RelicId);
            TestHelpers.AddRelic("FIDDLE");
            TestHelpers.SpawnCard("STRIKE_IRONCLAD", "draw");
            TestHelpers.DealDamageToPlayer(5);
        });
        runner.WaitFor(GameEvent.DamageReceived);
        runner.WaitUntil("the prevented puzzle trigger is used", () => TestHelpers.GetRelic<CentennialPuzzle>()!.UsedThisCombat);
        runner.Assert("a real blocked trigger credits zero cards", () =>
            new TestResult(Amount == 3 && TestHelpers.GetRelic<CentennialPuzzle>()!.UsedThisCombat,
                $"expected unchanged 3 and a used puzzle, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic("FIDDLE"); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }

#endif
}

// UnceasingTop: draws a card when hand empties
[HarmonyPatch(typeof(UnceasingTop), nameof(UnceasingTop.AfterHandEmptied))]
public sealed class UnceasingTopStats : MeasuredCounterStats<UnceasingTop>
{
    protected override string PreviousMeasurement => "requested draws";
    public override string Format => "Drew {0} cards from empty hand.";
    internal static void Prefix(UnceasingTop __instance, Player player, out DirectDrawScope? __state) =>
        __state = player == __instance.Owner
            ? DirectDrawScope.Begin(player, count => Track(__instance, s => s.Amount += count)) : null;

    internal static void Finalizer(DirectDrawScope? __state) => __state?.Dispose();

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("spawn and play card to empty hand", () =>
        {
            TestHelpers.EnableGodMode();
            TestHelpers.ProtectEnemy();
            TestHelpers.AddEnergy(10);
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.PlayThenEndTurn(1, 0);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        // Playing the only card empties the hand in the Play phase: CardModel.Play calls
        // CheckForEmptyHand after the card leaves play, the phase is valid, so the relic draws once
        // (the draw reshuffles the STRIKE back into hand). The end-of-turn flush never calls
        // CheckForEmptyHand, and at turn 2 the hand draw reshuffles the STRIKE in again, so the
        // auto-pre-play check sees a non-empty hand. Asserting at PlayerTurnStart (before that
        // check) also keeps the count independent of the reshuffle.
        runner.WaitUntil("the empty-hand draw completes", () => Amount == 1, 15000);
        runner.Assert("tracked one draw from the emptied hand", () =>
            new TestResult(Amount == 1, $"expected 1, got {Amount}"));
        // Negative: a card kept in hand through turn 2 means neither the auto-pre-play check nor
        // the flush empties the hand in a valid phase, so nothing is added by the end of turn 2.
        runner.Do("keep a card in hand and end turn 2", () => { TestHelpers.SpawnCard("DEFEND"); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.TurnEnd, 15000);
        runner.Assert("no draw while the hand stays populated", () =>
            new TestResult(Amount == 1, $"expected still 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// GamblingChip: discards and redraws cards turn 1
// Selected cards are credited only when the game records their actual discard.
[HarmonyPatch(typeof(GamblingChip), nameof(GamblingChip.AfterPlayerTurnStart))]
public sealed class GamblingChipStats : SimpleCounterStats<GamblingChip>
{
    public override string Format => "Discarded {0} selected cards.";
    public override StatCadence Cadence => StatCadence.Combat;
    public int CardsDiscarded { get; set; }
    private bool _changedDuringRun;

    protected override string FormatStat(int amount) => base.FormatStat(CardsDiscarded);
    public override string GetDescription(int effectiveTurns, int effectiveCombats)
    {
        var text = $"Discarded {Fmt.Blue(CardsDiscarded)} selected cards.";
        if (_changedDuringRun)
            return text + "\nMeasured since update.\nEarlier replacement draw requests: " + Fmt.Blue(Amount) + ".";
        return text + "\nPer combat: " +
            Fmt.Blue(((float)CardsDiscarded / Math.Max(effectiveCombats, 1)).ToString("0.###", CultureInfo.InvariantCulture));
    }
    public override JsonObject Save()
    {
        var data = base.Save();
        data["discardedCards"] = CardsDiscarded;
        data["changedDuringRun"] = _changedDuringRun;
        return data;
    }
    public override void Load(JsonObject data)
    {
        base.Load(data);
        CardsDiscarded = data["discardedCards"]?.GetValue<int>() ?? 0;
        _changedDuringRun = data["changedDuringRun"]?.GetValue<bool>() ?? data["discardedCards"] == null;
    }
    public override void Reset() { base.Reset(); CardsDiscarded = 0; _changedDuringRun = false; }

    internal static void Prefix(GamblingChip __instance, Player player, out ChipDiscardScope? __state) =>
        __state = player == __instance.Owner && player.PlayerCombatState!.TurnNumber <= 1
            ? ChipDiscardScope.Begin(player, () =>
            {
                if (__instance.IsMelted || !LocalContext.IsMine(__instance)) return;
                if (RelicStatsRegistry.Get(RelicIdHelper.Slugify(nameof(GamblingChip))) is GamblingChipStats stats)
                    stats.CardsDiscarded++;
            }) : null;

    internal static void Finalizer(ChipDiscardScope? __state) => __state?.Dispose();

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        Task? lethalDiscard = null;
        CardModel[] lethalSelected = Array.Empty<CardModel>();
        runner.Assert("legacy replacement requests have no new discard rates", () =>
        {
            var migrated = new GamblingChipStats();
            migrated.Load(new JsonObject { ["amount"] = 8 });
            migrated.CardsDiscarded = 3;
            var restored = new GamblingChipStats();
            restored.Load(migrated.Save());
            var text = restored.GetDescription(20, 10);
            return new TestResult(restored.Amount == 8 && restored.CardsDiscarded == 3 &&
                text.Contains("Measured since update") && text.Contains("Earlier replacement draw requests") &&
                !text.Contains("Per combat"),
                "expected actual discards separated from old requests without historical denominators");
        });
        // The chip prompts at turn-1 start, so the hand must already hold cards then. The deck is
        // cleared before every test, so Ninja Scroll (BeforeHandDraw, which runs before
        // AfterPlayerTurnStart in SetupPlayerTurn) supplies 3 Shivs; the auto selector picks all of
        // them and the chip calls DiscardAndDraw with that count.
        runner.Do("add relics and auto-answer the prompt", () =>
        {
            TestHelpers.AddRelic("NINJA_SCROLL");
            TestHelpers.AddRelic(RelicId);
            TestHelpers.PushAutoCardSelector();
        });
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.CardDiscarded, 15000);
        runner.WaitUntil("all three selected Shivs are discarded", () => CardsDiscarded == 3, 15000);
        runner.Assert("tracked the discarded selected Shivs", () =>
        {
            var scroll = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == "NINJA_SCROLL");
            var expected = scroll?.DynamicVars["Shivs"].IntValue ?? -1;
            return new TestResult(expected > 0 && CardsDiscarded == expected, $"expected {expected}, got {CardsDiscarded}");
        });
        // The chip only prompts on turn 1; turn 2 must not add anything.
        runner.Do("end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("no swap on turn 2", () =>
        {
            var scroll = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == "NINJA_SCROLL");
            var expected = scroll?.DynamicVars["Shivs"].IntValue ?? -1;
            return new TestResult(expected > 0 && CardsDiscarded == expected, $"expected still {expected}, got {CardsDiscarded}");
        });
        runner.Do("remove opening relics for controlled lethal test", () =>
        {
            TestHelpers.RemoveRelic("NINJA_SCROLL");
            TestHelpers.RemoveRelic(RelicId);
        });
        runner.Do("start an empty first turn", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Do("discard three selected cards with lethal Tingsha", () =>
        {
            TestHelpers.AddRelic(RelicId);
            TestHelpers.AddRelic("TINGSHA");
            foreach (var enemy in TestHelpers.Player!.Creature.CombatState!.HittableEnemies)
            {
                enemy.RemoveAllPowersInternalExcept();
                enemy.LoseBlockInternal(enemy.Block);
                enemy.SetCurrentHpInternal(1);
            }
            TestHelpers.DiscardHand();
            for (int i = 0; i < 3; i++) TestHelpers.SpawnCard("STRIKE_IRONCLAD");
            lethalSelected = PileType.Hand.GetPile(TestHelpers.Player!).Cards.ToArray();
            lethalDiscard = TestHelpers.GetRelic<GamblingChip>()!.AfterPlayerTurnStart(
                new ThrowingPlayerChoiceContext(), TestHelpers.Player!);
        });
        runner.WaitUntil("lethal selected-card discard resolves", () =>
        {
            if (lethalDiscard?.IsFaulted == true) throw lethalDiscard.Exception!;
            return lethalDiscard?.IsCompletedSuccessfully == true;
        }, 15000);
        runner.Assert("failed post-lethal moves do not count", () =>
            new TestResult(CardsDiscarded == 4 && lethalSelected.Count(card => card.Pile?.Type == PileType.Hand) == 2,
                $"expected total 4 completed discards and two cards still in Hand, got {CardsDiscarded}/" +
                lethalSelected.Count(card => card.Pile?.Type == PileType.Hand)));
        runner.Cleanup(() =>
        {
            TestHelpers.PopCardSelector();
            TestHelpers.RemoveRelic("TINGSHA");
            TestHelpers.RemoveRelic("NINJA_SCROLL");
            TestHelpers.RemoveRelic(RelicId);
            Reset();
        });
    }
#endif
}

// Bookmark: reduces cost of a random retained card after a flush
[HarmonyPatch(typeof(Bookmark), nameof(Bookmark.AfterFlush))]
public sealed class BookmarkStats : SimpleCounterStats<Bookmark>
{
    public override string Format => "Reduced card costs {0} times.";
    public static void Prefix(Bookmark __instance, Player player,
        IReadOnlyCollection<CardModel> retainedCards, out bool __state) =>
        __state = player == __instance.Owner && retainedCards.Any(
            c => !c.EnergyCost.CostsX && c.EnergyCost.GetWithModifiers(CostModifiers.Local) > 0);

    public static void Postfix(Bookmark __instance, bool __state)
    {
        if (__state) Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // AfterFlush receives the retained cards; the relic acts only when one of them has a fixed
        // cost > 0. Without Retain the STRIKE is flushed (negative); with Runic Pyramid, ShouldFlush
        // is false so the whole hand is retained and the cost-1 STRIKE qualifies.
        CardModel? reducedCard = null;
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("spawn a card and end turn without Retain", () =>
        {
            TestHelpers.EnableGodMode();
            TestHelpers.ProtectEnemy();
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.EndTurn();
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("nothing retained, nothing reduced", () =>
            new TestResult(Amount == 0, $"expected 0, got {Amount}"));
        runner.Do("add Runic Pyramid, spawn a card and end turn", () =>
        {
            TestHelpers.AddRelic("RUNIC_PYRAMID");
            PileType.Hand.GetPile(TestHelpers.Player!).Clear(silent: true);
            TestHelpers.SpawnCard("STRIKE_IRONCLAD");
            reducedCard = PileType.Hand.GetPile(TestHelpers.Player!).Cards.Single();
            TestHelpers.EndTurn();
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("counted the sole retained card becoming free", () =>
            new TestResult(Amount == 1 && reducedCard?.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0,
                $"expected one 1-to-0 reduction, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic("RUNIC_PYRAMID"); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// ── Conditional draw relics ────────────────────────────────────────────

// Pocketwatch: +3 cards if <= threshold cards played last turn
[HarmonyPatch(typeof(Pocketwatch), nameof(Pocketwatch.ModifyHandDraw))]
public sealed class PocketwatchStats : SimpleCounterStats<Pocketwatch>
{
    public override string Format => "Requested {0} extra cards from turn-start draw.";
    public static void Postfix(Pocketwatch __instance, Player player, decimal __result, decimal __1)
    {
        if (__result <= __1) return;
        Track(__instance, s => s.Amount += (int)(__result - __1));
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // ModifyHandDraw adds Cards when at most CardThreshold cards were played last turn, never on
        // turn 1. Turn 1 plays nothing -> turn 2 gets the bonus; turn 2 plays 4 (> 3) -> turn 3 does not.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("no bonus on turn 1", () =>
            new TestResult(Amount == 0, $"expected 0, got {Amount}"));
        runner.Do("end turn 1 without playing cards", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("bonus after a quiet turn", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Cards.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Do("play 4 cards on turn 2 and end it", () => {
            TestHelpers.EnableGodMode();
            TestHelpers.ProtectEnemy();
            TestHelpers.AddEnergy(10);
            for (int i = 0; i < 4; i++) TestHelpers.SpawnCard("STRIKE");
            TestHelpers.PlayThenEndTurn(4, 0);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("no bonus after exceeding the threshold", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Cards.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// PollinousCore: +2 cards every Nth turn
[HarmonyPatch(typeof(PollinousCore), nameof(PollinousCore.ModifyHandDraw))]
public sealed class PollinousCoreStats : SimpleCounterStats<PollinousCore>
{
    public override string Format => "Requested {0} extra cards from turn-start draw.";
    public static void Postfix(PollinousCore __instance, Player player, decimal __result, decimal __1)
    {
        if (__result <= __1) return;
        Track(__instance, s => s.Amount += (int)(__result - __1));
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // Not orb-based: BeforeHandDraw does TurnsSeen++ (turn N -> N), ModifyHandDraw adds Cards once
        // TurnsSeen >= Turns (4), and AfterModifyingHandDraw resets it. So turn 3's draw sees 3 (no
        // bonus) and turn 4's draw sees 4 (bonus). Both are visible at that turn's PlayerTurnStart.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("end turn 1", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Do("end turn 2", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("no bonus before the Nth turn", () =>
            new TestResult(Amount == 0, $"expected 0 at turn 3, got {Amount}"));
        runner.Do("end turn 3", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("bonus draw on the Nth turn", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Cards.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected} at turn 4, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
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
    private int _previousCost0, _previousCost1, _previousCost2, _previousCost3, _previousDiscount;
    private bool _hasPreviousMeasurement;

    public string GetDescription(int effectiveTurns, int effectiveCombats)
    {
        var totalCards = Cost0 + Cost1 + Cost2 + Cost3;
        var avgDiscount = totalCards > 0
            ? ((float)TotalDiscount / totalCards).ToString("0.##", CultureInfo.InvariantCulture)
            : "0";

        var text = $"Requested {Fmt.Blue(CardsDrawn)} extra cards from turn-start draw.\n" +
               $"Assigned roll counts (fixed-cost cards):\n" +
               $"  0 energy: {Fmt.Blue(Cost0)}  1 energy: {Fmt.Blue(Cost1)}\n" +
               $"  2 energy: {Fmt.Blue(Cost2)}  3 energy: {Fmt.Blue(Cost3)}\n" +
               $"Average roll discount vs upgraded base cost: {Fmt.Blue(avgDiscount)}";
        if (_hasPreviousMeasurement)
        {
            text += "\nRoll measurements recorded since update.\n" +
                    $"Earlier resolved cost counts (including X): 0={Fmt.Blue(_previousCost0)}, 1={Fmt.Blue(_previousCost1)}, " +
                    $"2={Fmt.Blue(_previousCost2)}, 3+={Fmt.Blue(_previousCost3)}.\n" +
                    $"Earlier canonical-cost discount total: {Fmt.Blue(_previousDiscount)}.";
        }
        return text;
    }

    public JsonObject Save()
    {
        var obj = new JsonObject
        {
            ["cardsDrawn"] = CardsDrawn,
            ["roll0"] = Cost0, ["roll1"] = Cost1,
            ["roll2"] = Cost2, ["roll3"] = Cost3,
            ["rollDiscount"] = TotalDiscount,
            ["rollMeasurementVersion"] = 1,
            ["hasPreviousMeasurement"] = _hasPreviousMeasurement,
            ["cost0"] = _previousCost0, ["cost1"] = _previousCost1,
            ["cost2"] = _previousCost2, ["cost3"] = _previousCost3,
            ["totalDiscount"] = _previousDiscount,
        };
        return obj;
    }

    public void Load(JsonObject data)
    {
        CardsDrawn = data["cardsDrawn"]?.GetValue<int>() ?? 0;
        Cost0 = data["roll0"]?.GetValue<int>() ?? 0;
        Cost1 = data["roll1"]?.GetValue<int>() ?? 0;
        Cost2 = data["roll2"]?.GetValue<int>() ?? 0;
        Cost3 = data["roll3"]?.GetValue<int>() ?? 0;
        TotalDiscount = data["rollDiscount"]?.GetValue<int>() ?? 0;
        _previousCost0 = data["cost0"]?.GetValue<int>() ?? 0;
        _previousCost1 = data["cost1"]?.GetValue<int>() ?? 0;
        _previousCost2 = data["cost2"]?.GetValue<int>() ?? 0;
        _previousCost3 = data["cost3"]?.GetValue<int>() ?? 0;
        _previousDiscount = data["totalDiscount"]?.GetValue<int>() ?? 0;
        _hasPreviousMeasurement = data["hasPreviousMeasurement"]?.GetValue<bool>() ??
            (data["rollMeasurementVersion"] == null && data["cost0"] != null);
    }

    public void Reset()
    {
        CardsDrawn = 0;
        Cost0 = Cost1 = Cost2 = Cost3 = 0;
        TotalDiscount = 0;
        _previousCost0 = _previousCost1 = _previousCost2 = _previousCost3 = _previousDiscount = 0;
        _hasPreviousMeasurement = false;
    }

    public static SneckoEyeStats? GetFor(SneckoEye instance)
    {
        if (instance.IsMelted) return null;
        if (!LocalContext.IsMine(instance)) return null;
        return RelicStatsRegistry.Get(RelicIdHelper.Slugify(nameof(SneckoEye))) as SneckoEyeStats;
    }

#if DEBUG
    public void RegisterTest(TestRunner runner)
    {
        runner.Assert("old cost measurements survive save/load separately", () =>
        {
            var migrated = new SneckoEyeStats();
            migrated.Load(new JsonObject { ["cardsDrawn"] = 8, ["cost0"] = 1, ["cost1"] = 2,
                ["cost2"] = 3, ["cost3"] = 4, ["totalDiscount"] = 7 });
            migrated.Cost2 = 1;
            var restored = new SneckoEyeStats();
            restored.Load(migrated.Save());
            var saved = restored.Save();
            return new TestResult(restored.CardsDrawn == 8 && restored.Cost0 == 0 && restored.Cost2 == 1 &&
                restored.TotalDiscount == 0 && saved["cost3"]?.GetValue<int>() == 4 &&
                saved["totalDiscount"]?.GetValue<int>() == 7 &&
                restored.GetDescription(10, 3).Contains("recorded since update"),
                "expected independent new roll measurements and preserved old bins/discount");
        });
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        // ModifyHandDraw adds Cards every turn (turns 1 and 2 -> 2 x Cards). The deck is empty, so
        // 7 STRIKEs seeded into the draw pile are exactly what turn 2's 5 + 2 hand draw pulls;
        // Confused re-rolls each drawn card's cost, so the cost tiers add up to 7.
        const int seeded = 7;
        runner.Do("seed the draw pile and end turn 1", () => {
            TestHelpers.EnableGodMode();
            TestHelpers.ProtectEnemy();
            for (int i = 0; i < seeded; i++) TestHelpers.SpawnCard("STRIKE_IRONCLAD", "draw");
            TestHelpers.EndTurn();
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked extra draws for two turns", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = 2 * (relic?.DynamicVars.Cards.IntValue ?? -1);
            return new TestResult(expected > 0 && CardsDrawn == expected, $"expected {expected}, got {CardsDrawn}");
        });
        runner.Assert("tracked a cost tier for every drawn card", () =>
        {
            var tiers = Cost0 + Cost1 + Cost2 + Cost3;
            return new TestResult(tiers == seeded, $"expected {seeded} tiered cards, got {tiers} ({Cost0}/{Cost1}/{Cost2}/{Cost3})");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
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
[HarmonyPatch(typeof(ConfusedPower),
    nameof(ConfusedPower.AfterCardDrawn))]
public static class SneckoEyeConfusionPatch
{
    public static void Prefix(ConfusedPower __instance, CardModel card, out int __state)
    {
        __state = card.Owner == __instance.Owner.Player && !card.EnergyCost.CostsX
            ? card.EnergyCost.GetWithModifiers(CostModifiers.None) : -1;
    }

    public static void Postfix(ConfusedPower __instance, CardModel card, int __state)
    {
        if (__state < 0 || card.Owner != __instance.Owner.Player) return;
        var relic = card.Owner.GetRelic<SneckoEye>();
        if (relic == null) return;
        var stats = SneckoEyeStats.GetFor(relic);
        if (stats == null) return;
        int roll = card.EnergyCost.GetWithModifiers(CostModifiers.Local);
        if (!RelicMeasurementMath.TryRollDiscount(card.EnergyCost.CostsX, __state, roll, out int discount)) return;
        switch (roll)
        {
            case 0: stats.Cost0++; break;
            case 1: stats.Cost1++; break;
            case 2: stats.Cost2++; break;
            case 3: stats.Cost3++; break;
        }
        stats.TotalDiscount += discount;
    }

}

// ── Draw-on-play-count relics ─────────────────────────────────────────

// IronClub: draws 1 card every 4 cards played
[HarmonyPatch(typeof(IronClub), nameof(IronClub.AfterCardPlayed))]
public sealed class IronClubStats : MeasuredCounterStats<IronClub>
{
    protected override string PreviousMeasurement => "requested draws";
    public override string Format => "Drew {0} cards.";
    internal static void Prefix(IronClub __instance, CardPlay cardPlay, out DirectDrawScope? __state) =>
        __state = cardPlay.Card.Owner == __instance.Owner
            ? DirectDrawScope.Begin(__instance.Owner, count => Track(__instance, s => s.Amount += count)) : null;

    internal static void Finalizer(DirectDrawScope? __state) => __state?.Dispose();

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        // Draws once every Cards (4) plays: nothing after 3, one after the 4th, nothing on the 5th.
        runner.Do("spawn 5 cards and play the first", () => {
            TestHelpers.EnableGodMode();
            TestHelpers.ProtectEnemy();
            TestHelpers.AddEnergy(10);
            for (int i = 0; i < 5; i++) TestHelpers.SpawnCard("STRIKE");
            TestHelpers.PlayCard(0, 0);
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Do("play card 2", () => TestHelpers.PlayCard(0, 0));
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Do("play card 3", () => TestHelpers.PlayCard(0, 0));
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("no draw after 3 plays", () =>
            new TestResult(Amount == 0, $"expected 0, got {Amount}"));
        runner.Do("play card 4", () => TestHelpers.PlayCard(0, 0));
        runner.WaitFor(GameEvent.CardPlayed);
        runner.WaitUntil("the fourth-card draw completes", () => Amount == 1, 15000);
        runner.Assert("tracked the draw on the 4th play", () =>
            new TestResult(Amount == 1, $"expected 1, got {Amount}"));
        runner.Do("play card 5", () => TestHelpers.PlayCard(0, 0));
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("no draw on the 5th play", () =>
            new TestResult(Amount == 1, $"expected still 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// ── Draw-on-exhaust relics ────────────────────────────────────────────

// JossPaper: draws cards every 5 exhausts
[HarmonyPatch]
public sealed class JossPaperStats : MeasuredCounterStats<JossPaper>
{
    protected override string PreviousMeasurement => "requested draws";
    public override string Format => "Drew {0} cards.";

    // Normal exhausts draw here; Ethereal exhausts are accumulated and draw at turn end.
    [HarmonyPatch(typeof(JossPaper), nameof(JossPaper.AfterCardExhausted))]
    [HarmonyPrefix]
    internal static void ExhaustPrefix(JossPaper __instance, CardModel card, bool causedByEthereal,
        out DirectDrawScope? __state)
    {
        __state = card.Owner == __instance.Owner && !causedByEthereal
            ? BeginDraw(__instance) : null;
    }

    [HarmonyPatch(typeof(JossPaper), nameof(JossPaper.AfterCardExhausted))]
    [HarmonyFinalizer]
    internal static void ExhaustFinished(DirectDrawScope? __state) => __state?.Dispose();

    [HarmonyPatch(typeof(JossPaper), nameof(JossPaper.AfterSideTurnEnd))]
    [HarmonyPrefix]
    internal static void TurnEndPrefix(JossPaper __instance, IEnumerable<Creature> participants,
        out DirectDrawScope? __state)
    {
        __state = participants.Contains(__instance.Owner.Creature) ? BeginDraw(__instance) : null;
    }

    [HarmonyPatch(typeof(JossPaper), nameof(JossPaper.AfterSideTurnEnd))]
    [HarmonyFinalizer]
    internal static void TurnEndFinished(DirectDrawScope? __state) => __state?.Dispose();

    private static DirectDrawScope BeginDraw(JossPaper relic) =>
        DirectDrawScope.Begin(relic.Owner, count => Track(relic, s => s.Amount += count));

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        // Seed the draw pile so the 5th exhaust's draw actually has something to draw.
        runner.Do("seed the draw pile", () =>
        {
            TestHelpers.SpawnCard("STRIKE_IRONCLAD", "draw");
            TestHelpers.SpawnCard("STRIKE_IRONCLAD", "draw");
        });
        for (int i = 0; i < 4; i++)
        {
            runner.Do($"exhaust card {i + 1}", () => { TestHelpers.SpawnCard("STRIKE"); TestHelpers.ExhaustCard(); });
            runner.WaitFor(GameEvent.CardExhausted);
        }
        runner.Assert("no draw after 4 exhausts", () =>
            new TestResult(Amount == 0, $"expected 0, got {Amount}"));
        runner.Do("exhaust card 5", () => { TestHelpers.SpawnCard("STRIKE"); TestHelpers.ExhaustCard(); });
        runner.WaitFor(GameEvent.CardExhausted);
        runner.WaitUntil("the threshold draw completed", () => Amount > 0);
        runner.Assert("tracked one draw on the 5th exhaust", () =>
            new TestResult(Amount == 1, $"expected 1, got {Amount}"));

        // Turn-end path: 3 normal exhausts leave the counter at 3; two Ethereal cards left in hand
        // are exhausted by the turn-end flush (not counted there), and AfterSideTurnEnd adds them to
        // reach 5 and draws one more card.
        for (int i = 0; i < 3; i++)
        {
            runner.Do($"exhaust card {i + 6}", () => { TestHelpers.SpawnCard("STRIKE"); TestHelpers.ExhaustCard(); });
            runner.WaitFor(GameEvent.CardExhausted);
        }
        runner.Do("keep two Ethereal cards in hand + end turn", () =>
        {
            TestHelpers.EnableGodMode();
            TestHelpers.ProtectEnemy();
            TestHelpers.SpawnCard("STRIKE_IRONCLAD", "draw");
            var hand = PileType.Hand.GetPile(TestHelpers.Player!);
            for (int i = 0; i < 2; i++)
            {
                TestHelpers.SpawnCard("DEFEND_IRONCLAD");
                CardCmd.ApplyKeyword(hand.Cards[hand.Cards.Count - 1], CardKeyword.Ethereal);
            }
            TestHelpers.EndTurn();
        });
        runner.WaitUntil("the turn-end draw completed", () => Amount > 1, 15000);
        runner.Assert("the Ethereal exhausts completed the count and drew one", () =>
            new TestResult(Amount == 2, $"expected 2, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// ── Auto-play relics ──────────────────────────────────────────────────

// HistoryCourse: auto-replays last Attack/Skill from previous turn
[HarmonyPatch(typeof(HistoryCourse), nameof(HistoryCourse.AfterAutoPrePlayPhaseEntered))]
public sealed class HistoryCourseStats : MeasuredCounterStats<HistoryCourse>
{
    protected override string PreviousMeasurement => "replay attempts";
    public override string Format => "Auto-replayed {0} cards.";
    internal static void Prefix(HistoryCourse __instance, Player player, out AutoPlayScope? __state) =>
        __state = player == __instance.Owner && player.PlayerCombatState!.TurnNumber > 1
            ? AutoPlayScope.Begin(player, () => Track(__instance, s => s.Amount++)) : null;

    internal static void Finalizer(AutoPlayScope? __state) => __state?.Dispose();

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        // An Attack played on turn 1 is replayed as a dupe in turn 2's auto-pre-play phase; the
        // dupe's own AfterCardPlayed is the sync point. The dupe is excluded from the lookup, so
        // turn 3 (nothing played manually on turn 2) replays nothing.
        runner.Do("play an attack on turn 1", () =>
        {
            TestHelpers.EnableGodMode();
            TestHelpers.ProtectEnemy();
            TestHelpers.AddEnergy(10);
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.PlayCard(0, 0);
        });
        runner.WaitFor(GameEvent.CardPlayed, 15000);
        runner.Do("end turn 1", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.WaitFor(GameEvent.CardPlayed, 15000);
        runner.Assert("tracked the turn-2 replay", () =>
            new TestResult(Amount == 1, $"expected 1, got {Amount}"));
        runner.Do("end turn 2 without playing", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Do("end turn 3", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.TurnEnd, 15000);
        runner.Assert("no replay when last turn had no manual attack", () =>
            new TestResult(Amount == 1, $"expected still 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// WhisperingEarring: auto-plays cards from hand on turn 1
[HarmonyPatch(typeof(WhisperingEarring), nameof(WhisperingEarring.AfterAutoPrePlayPhaseEnteredLate))]
public sealed class WhisperingEarringStats : SimpleCounterStats<WhisperingEarring>
{
    public int CardsAutoPlayed { get; set; }
    public int EnergyGenerated { get; set; }
    private bool _changedDuringRun;
    public override string GetDescription(int effectiveTurns, int effectiveCombats) =>
        $"Recorded {Fmt.Blue(CardsAutoPlayed)} cards auto-played. Recorded {Fmt.Blue(EnergyGenerated)} [gold]Energy[/gold] generated." +
        (_changedDuringRun ? "\nActual effects measured since update." : "") +
        $"\nTriggered {Fmt.Blue(Amount)} opening auto-play phases.";
    public override JsonObject Save()
    {
        var data = base.Save(); data["autoPlayed"] = CardsAutoPlayed; data["energy"] = EnergyGenerated;
        data["changedDuringRun"] = _changedDuringRun; return data;
    }
    public override void Load(JsonObject data)
    {
        base.Load(data); CardsAutoPlayed = data["autoPlayed"]?.GetValue<int>() ?? 0;
        EnergyGenerated = data["energy"]?.GetValue<int>() ?? 0;
        _changedDuringRun = data["changedDuringRun"]?.GetValue<bool>() ??
            (data["autoPlayed"] == null || data["energy"] == null);
    }
    public override void Reset() { base.Reset(); CardsAutoPlayed = 0; EnergyGenerated = 0; _changedDuringRun = false; }
    internal static void Prefix(WhisperingEarring __instance, Player player, out AutoPlayScope? __state) =>
        __state = player == __instance.Owner && player.PlayerCombatState!.TurnNumber <= 1
            ? AutoPlayScope.Begin(player, () => Track(__instance,
                stats => ((WhisperingEarringStats)stats).CardsAutoPlayed++)) : null;
    internal static void Finalizer(AutoPlayScope? __state) => __state?.Dispose();

    public override string Format => "Triggered {0} times.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(WhisperingEarring __instance, Player player)
    {
        if (player != __instance.Owner) return;
        if (player.PlayerCombatState!.TurnNumber > 1) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // AfterAutoPrePlayPhaseEnteredLate runs after PlayerTurnStart and before the Play phase, so
        // TurnEnd is the first sync point past it. Amount counts triggers (turn 1 only), not cards
        // played: with an empty hand it triggers and auto-plays nothing.
        runner.Assert("legacy opening phases survive separately from actual effects", () =>
        {
            var migrated = new WhisperingEarringStats();
            migrated.Load(new JsonObject { ["amount"] = 8 });
            var restored = new WhisperingEarringStats();
            restored.Load(migrated.Save());
            return new TestResult(restored.Amount == 8 && restored.CardsAutoPlayed == 0 && restored.EnergyGenerated == 0 &&
                restored.GetDescription(20, 10).Contains("measured since update"),
                "expected earlier phase count 8 and independently recorded actual effects");
        });
        runner.Do("add relics and one Power to the deck", () =>
        {
            TestHelpers.AddRelic(RelicId);
            TestHelpers.AddRelic("SPIKED_GAUNTLETS");
            RelicStatsRegistry.Get(RelicIdHelper.Slugify(nameof(SpikedGauntlets)))?.Reset();
            TestHelpers.AddCardToDeck("DEMON_FORM");
        });
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.WaitUntil("Earring's real Power auto-play completes", () => CardsAutoPlayed == 1, 15000);
        runner.Assert("actual auto-play and Energy benefit, including prepaid surcharge", () =>
        {
            var gauntlets = RelicStatsRegistry.Get(RelicIdHelper.Slugify(nameof(SpikedGauntlets))) as SpikedGauntletsStats;
            return new TestResult(CardsAutoPlayed == 1 && EnergyGenerated == 1 && gauntlets?.PowerCostIncrease == 1,
                $"expected one auto-play, one Earring Energy, and one prepaid surcharge; got {CardsAutoPlayed}/{EnergyGenerated}/{gauntlets?.PowerCostIncrease}");
        });
        runner.Do("end turn 1", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.TurnEnd, 15000);
        runner.Assert("triggered once on turn 1", () =>
            new TestResult(Amount == 1, $"expected 1, got {Amount}"));
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Do("end turn 2", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.TurnEnd, 15000);
        runner.Assert("no trigger on turn 2", () =>
            new TestResult(Amount == 1, $"expected still 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic("SPIKED_GAUNTLETS"); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// ── Card reward modifier relics ───────────────────────────────────────

// LastingCandy: adds an extra Power card to rewards every other combat
[HarmonyPatch(typeof(LastingCandy), nameof(LastingCandy.TryModifyCardRewardOptions))]
public sealed class LastingCandyStats : SimpleCounterStats<LastingCandy>
{
    public override string Format => "Added {0} extra Power cards to rewards.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(LastingCandy __instance, Player player, bool __result)
    {
        if (!__result) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // The relic acts on combat card rewards only in "triggering" combats, i.e. when its public
        // CombatRewardsSeen counter is odd (it is incremented by the rewards hook, which
        // GenerateCardReward does not run, so the value we set is what the relic sees).
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("generate a reward in a triggering combat", () =>
        {
            AccessTools.Property(typeof(LastingCandy), "CombatRewardsSeen")?.SetValue(TestHelpers.GetRelic<LastingCandy>(), 1);
            TestHelpers.GenerateCardReward();
        });
        runner.Assert("added a Power card once", () =>
            new TestResult(Amount == 1, $"expected 1, got {Amount}"));
        runner.Do("generate a reward in a non-triggering combat", () =>
        {
            AccessTools.Property(typeof(LastingCandy), "CombatRewardsSeen")?.SetValue(TestHelpers.GetRelic<LastingCandy>(), 2);
            TestHelpers.GenerateCardReward();
        });
        runner.Assert("no card added on an even combat", () =>
            new TestResult(Amount == 1, $"expected still 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// SilverCrucible: upgrades all card rewards (limited uses)
[HarmonyPatch(typeof(SilverCrucible), nameof(SilverCrucible.TryModifyCardRewardOptionsLate))]
public sealed class SilverCrucibleStats : SimpleCounterStats<SilverCrucible>
{
    public override string Format => "Processed {0} card reward upgrade batches.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(SilverCrucible __instance, Player player, bool __result)
    {
        if (!__result) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // Upgrades every card reward while TimesUsed < Cards (3); AfterModifyingCardRewardOptions
        // increments TimesUsed, so the 4th reward is left alone.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("generate three card rewards", () =>
        {
            for (int i = 0; i < 3; i++) TestHelpers.GenerateCardReward();
        });
        runner.Assert("upgraded three rewards", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Cards.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Do("generate a fourth card reward", () => TestHelpers.GenerateCardReward());
        runner.Assert("no upgrade once used up", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Cards.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// ── New relics (0.109.0) ───────────────────────────────────────────────

// NOTE: DowsingRod is a 0.108/0.109-beta-only relic; omitted so the mod loads on stable (0.107.1).

[HarmonyPatch(typeof(WhisperingEarring), nameof(WhisperingEarring.ModifyMaxEnergy))]
internal static class WhisperingEarringEnergyPatch
{
    public static void Postfix(WhisperingEarring __instance, decimal __result, decimal __1)
    {
        if (!EnergyGrantScope.IsCounting || __result <= __1) return;
        SimpleCounterStats<WhisperingEarring>.Track(__instance,
            stats => ((WhisperingEarringStats)stats).EnergyGenerated += (int)(__result - __1));
    }
}
