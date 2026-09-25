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
    public override string Format => "Drew {0} additional cards.";
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
    public override string Format => "Drew {0} fewer cards.";
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
    public override string Format => "Drew {0} additional cards.";
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
    public override string Format => "Drew {0} additional cards.";

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
    public override string Format => "Drew {0} additional cards.";
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
    public override string Format => "Drew {0} additional cards.";
    public override StatCadence Cadence => StatCadence.Total;
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
    public override string Format => "Drew {0} additional cards.";
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

// RingingTriangle: retains hand turn 1 only
[HarmonyPatch(typeof(RingingTriangle), nameof(RingingTriangle.ShouldFlush))]
public sealed class RingingTriangleStats : SimpleCounterStats<RingingTriangle>
{
    public override string Format => "Retained hand {0} times.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(RingingTriangle __instance, Player player, bool __result)
    {
        // ShouldFlush returns false when retaining
        if (__result) return;
        if (player != __instance.Owner) return;
        Track(__instance, s => s.Amount++);
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
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.SpawnCard("DEFEND");
            TestHelpers.EndTurn();
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("retained the turn-1 hand once", () =>
            new TestResult(Amount == 1, $"expected 1, got {Amount}"));
        // ShouldFlush only returns false on turn 1; the turn-2 flush must not count.
        runner.Do("end turn 2", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("no retention from turn 2 on", () =>
            new TestResult(Amount == 1, $"expected still 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
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

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("nothing retained before the first flush", () =>
            new TestResult(Amount == 0, $"expected 0, got {Amount}"));
        runner.Do("spawn cards and end turn 1", () =>
        {
            TestHelpers.EnableGodMode();
            TestHelpers.ProtectEnemy();
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.SpawnCard("DEFEND");
            TestHelpers.EndTurn();
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("retained once after turn 1", () =>
            new TestResult(Amount == 1, $"expected 1, got {Amount}"));
        runner.Do("end turn 2", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("retained again after turn 2", () =>
            new TestResult(Amount == 2, $"expected 2, got {Amount}"));
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
    public static void Postfix(OrangeDough __instance, CombatSide side, ICombatState combatState)
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
public sealed class CentennialPuzzleStats : SimpleCounterStats<CentennialPuzzle>
{
    public override string Format => "Drew {0} cards on hit.";
    public override StatCadence Cadence => StatCadence.Total;

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

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("end turn to let enemy attack", () => { TestHelpers.Heal(999); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked cards drawn", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = (int)(relic?.DynamicVars.Cards.BaseValue ?? -1);
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        // UsedThisCombat is set on the first unblocked hit; a second hit in the same combat is ignored.
        runner.Do("end turn again to take a second hit", () => { TestHelpers.Heal(999); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("no increment on a second hit", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = (int)(relic?.DynamicVars.Cards.BaseValue ?? -1);
            return new TestResult(expected > 0 && Amount == expected, $"expected still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// UnceasingTop: draws a card when hand empties
[HarmonyPatch(typeof(UnceasingTop), nameof(UnceasingTop.AfterHandEmptied))]
public sealed class UnceasingTopStats : SimpleCounterStats<UnceasingTop>
{
    public override string Format => "Drew {0} cards from empty hand.";
    public static void Postfix(UnceasingTop __instance, Player player)
    {
        if (player != __instance.Owner) return;
        // Mirror the relic's own guard: it only draws during AutoPrePlay/Play/AutoPostPlay
        // (skips Start/End so an autoplay-emptied hand doesn't always trigger a draw).
        var phase = player.PlayerCombatState!.Phase;
        if (phase != PlayerTurnPhase.AutoPrePlay && phase != PlayerTurnPhase.Play && phase != PlayerTurnPhase.AutoPostPlay) return;
        Track(__instance, s => s.Amount++);
    }

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
// Two patches work together: the Prefix on AfterPlayerTurnStart sets a flag with the relic
// instance, and the Prefix on CardCmd.DiscardAndDraw checks that flag to track the actual
// number of cards swapped (not just that the effect triggered).
[HarmonyPatch(typeof(GamblingChip), nameof(GamblingChip.AfterPlayerTurnStart))]
public sealed class GamblingChipStats : SimpleCounterStats<GamblingChip>
{
    public override string Format => "Swapped {0} cards.";
    internal static GamblingChip? ActiveInstance;

    public static void Prefix(GamblingChip __instance, Player player)
    {
        if (player != __instance.Owner) return;
        if (__instance.Owner.PlayerCombatState!.TurnNumber > 1) return;
        ActiveInstance = __instance;
    }

    // The relic's AfterPlayerTurnStart awaits the discard prompt and only calls
    // CardCmd.DiscardAndDraw when at least one card was picked. Clearing the flag solely in the
    // DiscardAndDraw prefix therefore leaks it on a pick of 0 cards, and the next DiscardAndDraw from
    // any source (a card, a potion) would be credited to the chip. A plain Postfix cannot clear it
    // either: on an async method it runs when the method first yields, i.e. while the prompt is
    // still open. Wrapping the returned Task instead ties the flag's lifetime exactly to the
    // method's own: it is cleared when the relic's turn-start work completes, for 0 or N cards.
    public static void Postfix(GamblingChip __instance, ref Task __result)
    {
        if (ActiveInstance != __instance) return;
        __result = ClearWhenDone(__result);
    }

    private static async Task ClearWhenDone(Task inner)
    {
        try { await inner; }
        finally { ActiveInstance = null; }
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
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
        runner.Assert("tracked the swapped Shivs", () =>
        {
            var scroll = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == "NINJA_SCROLL");
            var expected = scroll?.DynamicVars["Shivs"].IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        // The chip only prompts on turn 1; turn 2 must not add anything.
        runner.Do("end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("no swap on turn 2", () =>
        {
            var scroll = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == "NINJA_SCROLL");
            var expected = scroll?.DynamicVars["Shivs"].IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected still {expected}, got {Amount}");
        });
        runner.Cleanup(() =>
        {
            TestHelpers.PopCardSelector();
            ActiveInstance = null;
            TestHelpers.RemoveRelic("NINJA_SCROLL");
            TestHelpers.RemoveRelic(RelicId);
            Reset();
        });
    }
#endif
}

[HarmonyPatch(typeof(CardCmd), nameof(CardCmd.DiscardAndDraw))]
public static class GamblingChipDiscardAndDrawPatch
{
    public static void Prefix(int cardsToDraw)
    {
        var instance = GamblingChipStats.ActiveInstance;
        if (instance == null) return;
        GamblingChipStats.ActiveInstance = null;
        if (cardsToDraw <= 0) return;
        SimpleCounterStats<GamblingChip>.Track(instance, s => s.Amount += cardsToDraw);
    }
}

// Bookmark: reduces cost of a random retained card after a flush
[HarmonyPatch(typeof(Bookmark), nameof(Bookmark.AfterFlush))]
public sealed class BookmarkStats : SimpleCounterStats<Bookmark>
{
    public override string Format => "Reduced card costs {0} times.";
    public static void Postfix(Bookmark __instance, Player player, IReadOnlyCollection<CardModel> retainedCards)
    {
        if (player != __instance.Owner) return;
        // The original only acts when there's at least one retained card with a fixed cost > 0.
        // We replicate the check to avoid false positives.
        bool anyEligible = retainedCards.Any(
            c => !c.EnergyCost.CostsX && c.EnergyCost.GetWithModifiers(CostModifiers.Local) > 0);
        if (!anyEligible) return;
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // AfterFlush receives the retained cards; the relic acts only when one of them has a fixed
        // cost > 0. Without Retain the STRIKE is flushed (negative); with Runic Pyramid, ShouldFlush
        // is false so the whole hand is retained and the cost-1 STRIKE qualifies.
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
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.EndTurn();
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("reduced a retained card once", () =>
            new TestResult(Amount == 1, $"expected 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic("RUNIC_PYRAMID"); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
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
    public override string Format => "Drew {0} additional cards.";
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

    public string GetDescription(int effectiveTurns, int effectiveCombats)
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
        };
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
    }

    public void Reset()
    {
        CardsDrawn = 0;
        Cost0 = Cost1 = Cost2 = Cost3 = 0;
        TotalDiscount = 0;
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
    public static void Postfix(ConfusedPower __instance,
        PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card.Owner == null) return;
        if (card.Owner != __instance.Owner.Player) return;

        var sneckoEye = card.Owner.GetRelic<SneckoEye>();
        if (sneckoEye == null) return;

        var stats = SneckoEyeStats.GetFor(sneckoEye);
        if (stats == null) return;

        int originalCost = card.EnergyCost.Canonical;
        if (originalCost < 0) return;

        int newCost = card.EnergyCost.GetResolved();
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

// ── Draw-on-play-count relics ─────────────────────────────────────────

// IronClub: draws 1 card every 4 cards played
[HarmonyPatch(typeof(IronClub), nameof(IronClub.AfterCardPlayed))]
public sealed class IronClubStats : SimpleCounterStats<IronClub>
{
    public override string Format => "Drew {0} cards.";
    public static void Postfix(IronClub __instance, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != __instance.Owner) return;
        if (__instance.CardsPlayed % __instance.DynamicVars.Cards.IntValue != 0) return;
        if (!CombatManager.Instance.IsInProgress) return;
        Track(__instance, s => s.Amount++);
    }

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
[HarmonyPatch(typeof(JossPaper), nameof(JossPaper.AfterCardExhausted))]
public sealed class JossPaperStats : SimpleCounterStats<JossPaper>
{
    public override string Format => "Drew {0} cards.";

    [ThreadStatic] private static int _exhaustedBefore;

    public static void Prefix(JossPaper __instance)
    {
        _exhaustedBefore = __instance.CardsExhausted;
    }

    public static void Postfix(JossPaper __instance, CardModel card, bool causedByEthereal)
    {
        if (card.Owner != __instance.Owner) return;
        if (causedByEthereal) return;
        // The game does CardsExhausted++, then DrawIfThresholdMet awaits CardPileCmd.Draw and only
        // afterwards does CardsExhausted %= ExhaustAmount. When there is nothing to draw, the Draw
        // completes synchronously, so by the time this Postfix runs the counter has already wrapped
        // to 0 and reading it here undercounts. Replay the game's arithmetic on the value captured
        // before the increment instead. The draw count is CardsExhausted / ExhaustAmount, exactly as
        // DrawIfThresholdMet computes it (CardsVar(1) is display-only).
        int threshold = __instance.DynamicVars["ExhaustAmount"].IntValue;
        int after = _exhaustedBefore + 1;
        if (after < threshold) return;
        int drawn = after / threshold;
        Track(__instance, s => s.Amount += drawn);
    }

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
        runner.Assert("tracked one draw on the 5th exhaust", () =>
            new TestResult(Amount == 1, $"expected 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// ── Auto-play relics ──────────────────────────────────────────────────

// HistoryCourse: auto-replays last Attack/Skill from previous turn
[HarmonyPatch(typeof(HistoryCourse), nameof(HistoryCourse.AfterAutoPrePlayPhaseEntered))]
public sealed class HistoryCourseStats : SimpleCounterStats<HistoryCourse>
{
    public override string Format => "Auto-replayed {0} cards.";
    public static void Postfix(HistoryCourse __instance, Player player)
    {
        if (player != __instance.Owner) return;
        if (player.PlayerCombatState!.TurnNumber == 1) return;
        // The relic only replays when its history lookup finds a non-dupe Attack the owner played
        // last turn; a turn with nothing to replay must not count. Same expression as the game's.
        var owner = __instance.Owner;
        bool replayed = CombatManager.Instance.History.CardPlaysFinished.Any(
            (CardPlayFinishedEntry e) => e.CardPlay.Player == owner && e.HappenedLastPlayerTurn(owner)
                && e.CardPlay.Card.Type == CardType.Attack && !e.CardPlay.Card.IsDupe);
        if (!replayed) return;
        Track(__instance, s => s.Amount++);
    }

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
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("end turn 1", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.TurnEnd, 15000);
        runner.Assert("triggered once on turn 1", () =>
            new TestResult(Amount == 1, $"expected 1, got {Amount}"));
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Do("end turn 2", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.TurnEnd, 15000);
        runner.Assert("no trigger on turn 2", () =>
            new TestResult(Amount == 1, $"expected still 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
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
            TestHelpers.GetRelic<LastingCandy>()!.CombatRewardsSeen = 1;
            TestHelpers.GenerateCardReward();
        });
        runner.Assert("added a Power card once", () =>
            new TestResult(Amount == 1, $"expected 1, got {Amount}"));
        runner.Do("generate a reward in a non-triggering combat", () =>
        {
            TestHelpers.GetRelic<LastingCandy>()!.CombatRewardsSeen = 2;
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
    public override string Format => "Upgraded card rewards {0} times.";
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

// SilkenTress: enchants a card reward with Glam once, then disables (you lose all gold on pickup)
[HarmonyPatch(typeof(SilkenTress), nameof(SilkenTress.TryModifyCardRewardOptionsLate))]
public sealed class SilkenTressStats : SimpleCounterStats<SilkenTress>
{
    public override string Format => "Enchanted {0} card rewards with [gold]Glam[/gold].";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(SilkenTress __instance, Player player, List<CardCreationResult> cardRewards, bool __result)
    {
        if (!__result) return;
        if (player != __instance.Owner) return;
        Track(__instance, s => s.Amount += cardRewards.Count);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // Amount counts the reward options offered on the one reward the relic enchants (the whole
        // cardRewards list, 3 options); AfterModifyingCardRewardOptions then sets IsUsed, so a
        // second reward is untouched.
        int options = 0;
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("generate a card reward", () => options = TestHelpers.GenerateCardReward().Cards.Count());
        runner.Assert("counted the enchanted reward's options", () =>
            new TestResult(options == 3 && Amount == options, $"expected {options} (3 options), got {Amount}"));
        runner.Do("generate a second card reward", () => TestHelpers.GenerateCardReward());
        runner.Assert("no second enchant once used", () =>
            new TestResult(Amount == options, $"expected still {options}, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// NOTE: DowsingRod is a 0.108/0.109-beta-only relic; omitted so the mod loads on stable (0.107.1).

// HeftyTablet: offers rare cards to add (plus an Injury) on pickup
[HarmonyPatch(typeof(HeftyTablet), nameof(HeftyTablet.AfterObtained))]
public sealed class HeftyTabletStats : SimpleCounterStats<HeftyTablet>
{
    public override string Format => "Offered {0} rare cards.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(HeftyTablet __instance) =>
        Track(__instance, s => s.Amount += __instance.DynamicVars.Cards.IntValue);

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // AfterObtained only runs on a real pickup (RelicCmd.Obtain); the silent add never fires it.
        // The pickup opens a choose-a-card screen, answered by the auto selector.
        runner.Do("add relic silently", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("silent add offers nothing", () =>
            new TestResult(Amount == 0, $"expected 0, got {Amount}"));
        runner.Do("pick the relic up for real", () =>
        {
            TestHelpers.RemoveRelic(RelicId);
            TestHelpers.PushAutoCardSelector();
            TestHelpers.ObtainRelic(RelicId);
        });
        runner.Assert("offered Cards rare cards on pickup", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Cards.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.PopCardSelector(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// NeowsTalisman: upgrades starter Strike & Defend on pickup
[HarmonyPatch(typeof(NeowsTalisman), nameof(NeowsTalisman.AfterObtained))]
public sealed class NeowsTalismanStats : SimpleCounterStats<NeowsTalisman>
{
    public override string Format => "Upgraded {0} starter cards.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(NeowsTalisman __instance)
    {
        var basics = PileType.Deck.GetPile(__instance.Owner).Cards
            .Where(c => c.Rarity == CardRarity.Basic).ToList();
        int count = 0;
        if (basics.Any(c => c.Tags.Contains(CardTag.Strike))) count++;
        if (basics.Any(c => c.Tags.Contains(CardTag.Defend))) count++;
        if (count == 0) return;
        Track(__instance, s => s.Amount += count);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // AfterObtained upgrades the last Basic Strike and Defend in the deck. The deck is cleared
        // before every test, so add one of each (AddCardToDeck needs a combat room) before the
        // real pickup; the silent add never fires the hook.
        runner.Do("add relic silently", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("silent add upgrades nothing", () =>
            new TestResult(Amount == 0, $"expected 0, got {Amount}"));
        runner.Do("add a Strike to the deck", () => TestHelpers.AddCardToDeck("STRIKE_IRONCLAD"));
        runner.WaitFor(GameEvent.CardChangedPiles);
        runner.Do("add a Defend to the deck", () => TestHelpers.AddCardToDeck("DEFEND_IRONCLAD"));
        runner.WaitFor(GameEvent.CardChangedPiles);
        runner.Do("pick the relic up for real", () =>
        {
            TestHelpers.RemoveRelic(RelicId);
            TestHelpers.ObtainRelic(RelicId);
        });
        runner.Assert("upgraded one Strike and one Defend", () =>
            new TestResult(Amount == 2, $"expected 2, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// Kaleidoscope: offers cross-character card rewards on pickup
[HarmonyPatch(typeof(Kaleidoscope), nameof(Kaleidoscope.AfterObtained))]
public sealed class KaleidoscopeStats : SimpleCounterStats<Kaleidoscope>
{
    public override string Format => "Offered {0} cross-character card rewards.";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(Kaleidoscope __instance) =>
        Track(__instance, s => s.Amount += __instance.DynamicVars.Cards.IntValue);

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // AfterObtained offers Cards (2) cross-character card rewards through a rewards screen on a
        // real pickup only; the silent add never fires it. The screen is closed in Cleanup.
        runner.Do("add relic silently", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("silent add offers nothing", () =>
            new TestResult(Amount == 0, $"expected 0, got {Amount}"));
        runner.Do("pick the relic up for real", () =>
        {
            TestHelpers.RemoveRelic(RelicId);
            TestHelpers.ObtainRelic(RelicId);
        });
        runner.Assert("offered Cards rewards on pickup", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Cards.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.CloseOverlays(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}
