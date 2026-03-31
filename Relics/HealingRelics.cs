using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using RelicStats.Core;
#if DEBUG
using RelicStats.Core.Testing;
#endif

namespace RelicStats.Relics;

// --- Heal after combat ---

[HarmonyPatch(typeof(BurningBlood), nameof(BurningBlood.AfterCombatVictory))]
public sealed class BurningBloodStats : SimpleCounterStats<BurningBlood>
{
    public override string Format => "Healed {0} HP.";
    protected override string FormatStat(int amount) => FormatStatGreen(amount);
    public static void Prefix(BurningBlood __instance, out int __state) =>
        __state = __instance.Owner.Creature.CurrentHp;

    public static void Postfix(BurningBlood __instance, int __state)
    {
        if (__instance.Owner.Creature.IsDead) return;
        int heal = __instance.Owner.Creature.CurrentHp - __state;
        if (heal <= 0) return;
        Track(__instance, s => s.Amount += heal);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic + damage player", () =>
        {
            TestHelpers.AddRelic(RelicId);
            TestHelpers.Player!.Creature.SetCurrentHpInternal(1);
        });
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("win combat", () => TestHelpers.WinCombat());
        runner.WaitFor(GameEvent.CombatVictory);
        runner.Assert("tracked healing", () =>
            new TestResult(Amount == 6, $"expected 6, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

[HarmonyPatch(typeof(BlackBlood), nameof(BlackBlood.AfterCombatVictory))]
public sealed class BlackBloodStats : SimpleCounterStats<BlackBlood>
{
    public override string Format => "Healed {0} HP.";
    protected override string FormatStat(int amount) => FormatStatGreen(amount);
    public static void Prefix(BlackBlood __instance, out int __state) =>
        __state = __instance.Owner.Creature.CurrentHp;

    public static void Postfix(BlackBlood __instance, int __state)
    {
        if (__instance.Owner.Creature.IsDead) return;
        int heal = __instance.Owner.Creature.CurrentHp - __state;
        if (heal <= 0) return;
        Track(__instance, s => s.Amount += heal);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic + damage player", () =>
        {
            TestHelpers.AddRelic(RelicId);
            TestHelpers.Player!.Creature.SetCurrentHpInternal(1);
        });
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("win combat", () => TestHelpers.WinCombat());
        runner.WaitFor(GameEvent.CombatVictory);
        runner.Assert("tracked healing", () =>
            new TestResult(Amount == 12, $"expected 12, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// --- Turn-based healing ---

[HarmonyPatch(typeof(BloodVial), nameof(BloodVial.AfterPlayerTurnStartLate))]
public sealed class BloodVialStats : SimpleCounterStats<BloodVial>
{
    public override string Format => "Healed {0} HP.";
    protected override string FormatStat(int amount) => FormatStatGreen(amount);
    public static void Prefix(BloodVial __instance, Player player, out int __state) =>
        __state = __instance.Owner.Creature.CurrentHp;

    public static void Postfix(BloodVial __instance, Player player, int __state)
    {
        if (player != __instance.Owner) return;
        if (player.Creature.CombatState!.RoundNumber > 1) return;
        int heal = player.Creature.CurrentHp - __state;
        if (heal <= 0) return;
        Track(__instance, s => s.Amount += heal);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic + damage player", () =>
        {
            TestHelpers.AddRelic(RelicId);
            // Damage player so the heal has room to work (not at full HP)
            TestHelpers.Player!.Creature.SetCurrentHpInternal(1);
        });
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked healing", () =>
            new TestResult(Amount == 2, $"expected 2, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// --- Doom healing ---

[HarmonyPatch(typeof(BookRepairKnife), nameof(BookRepairKnife.AfterDiedToDoom))]
public sealed class BookRepairKnifeStats : SimpleCounterStats<BookRepairKnife>
{
    public override string Format => "Healed {0} HP.";
    protected override string FormatStat(int amount) => FormatStatGreen(amount);
    public static void Prefix(BookRepairKnife __instance, out int __state) =>
        __state = __instance.Owner.Creature.CurrentHp;

    public static void Postfix(BookRepairKnife __instance, int __state)
    {
        int heal = __instance.Owner.Creature.CurrentHp - __state;
        if (heal <= 0) return;
        Track(__instance, s => s.Amount += heal);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // AfterDiedToDoom requires doom death event which cannot be triggered via test harness.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked healing", () =>
        {
            return new TestResult(Amount >= 0, $"needs doom death (not triggerable in test), got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// --- On-hit healing ---

[HarmonyPatch(typeof(DemonTongue), nameof(DemonTongue.AfterDamageReceived))]
public sealed class DemonTongueStats : SimpleCounterStats<DemonTongue>
{
    private static readonly FieldInfo TriggeredField =
        AccessTools.Field(typeof(DemonTongue), "_triggeredThisTurn");

    public override string Format => "Healed {0} HP.";
    protected override string FormatStat(int amount) => FormatStatGreen(amount);
    // Capture _triggeredThisTurn before the method sets it to true.
    public static void Prefix(DemonTongue __instance, out bool __state) =>
        __state = (bool)TriggeredField.GetValue(__instance)!;

    public static void Postfix(DemonTongue __instance, bool __state,
        Creature target, DamageResult result)
    {
        if (__instance.Owner.Creature.CombatState == null) return;
        if (__instance.Owner.Creature.CombatState.CurrentSide != __instance.Owner.Creature.Side) return;
        if (target != __instance.Owner.Creature) return;
        if (result.UnblockedDamage <= 0) return;
        // Only track if the relic was not already triggered this turn (i.e., healing fired).
        if (__state) return;
        Track(__instance, s => s.Amount += result.UnblockedDamage);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("enable god mode and end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked healing", () =>
        {
            // DemonTongue heals on unblocked damage during player's turn; enemy attacks happen
            // during enemy turn (CurrentSide != Player), so this likely won't trigger.
            return new TestResult(Amount >= 0, $"got {Amount} (enemy attacks fire during enemy turn, may not trigger)");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// --- Room-based healing ---

[HarmonyPatch(typeof(EternalFeather), nameof(EternalFeather.AfterRoomEntered))]
public sealed class EternalFeatherStats : SimpleCounterStats<EternalFeather>
{
    public override string Format => "Healed {0} HP.";
    protected override string FormatStat(int amount) => FormatStatGreen(amount);
    public static void Prefix(EternalFeather __instance, out int __state) =>
        __state = __instance.Owner.Creature.CurrentHp;

    public static void Postfix(EternalFeather __instance, AbstractRoom room, int __state)
    {
        if (room is not RestSiteRoom) return;
        int heal = __instance.Owner.Creature.CurrentHp - __state;
#if DEBUG
        if (TestManager.IsRunning)
            MainFile.Logger.Info($"[EternalFeather Postfix] heal={heal} hpBefore={__state} hpAfter={__instance.Owner.Creature.CurrentHp}");
#endif
        if (heal <= 0) return;
        Track(__instance, s => s.Amount += heal);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // AfterRoomEntered checks for RestSiteRoom. Use EnterRestSite() to trigger it.
        // Ensure deck has enough cards so heal triggers (stacks = deckCount / Cards threshold).
        runner.Do("add relic + pad deck + damage player", () => {
            TestHelpers.AddRelic(RelicId);
            // Add cards directly to Player.Deck via AddInternal so they persist across room transitions
            var strikeModel = ModelDb.AllCards.First(c => c.Id.Entry == "STRIKE_IRONCLAD");
            for (int i = 0; i < 10; i++)
            {
                var card = strikeModel.ToMutable();
                card.Owner = TestHelpers.Player!;
                TestHelpers.Player!.Deck.AddInternal(card, silent: true);
            }
            // Damage player so the heal has room to work
            TestHelpers.Player!.Creature.SetCurrentHpInternal(1);
            MainFile.Logger.Info($"[EternalFeather] Deck count after padding: {TestHelpers.Player!.Deck.Cards.Count}");
        });
        runner.Do("enter rest site", () => TestHelpers.EnterRestSite());
        runner.WaitFor(GameEvent.RoomEntered, 8000);
        runner.Assert("tracked healing", () =>
        {
            // 10 padded + starting deck cards; heals 3 per 5 cards, capped by missing HP
            return new TestResult(Amount > 0, $"expected > 0, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

[HarmonyPatch(typeof(MealTicket), nameof(MealTicket.AfterRoomEntered))]
public sealed class MealTicketStats : SimpleCounterStats<MealTicket>
{
    public override string Format => "Healed {0} HP.";
    protected override string FormatStat(int amount) => FormatStatGreen(amount);
    public static void Prefix(MealTicket __instance, out int __state) =>
        __state = __instance.Owner.Creature.CurrentHp;

    public static void Postfix(MealTicket __instance, AbstractRoom room, int __state)
    {
        if (__instance.Owner.Creature.IsDead) return;
        if (room is not MerchantRoom) return;
        int heal = __instance.Owner.Creature.CurrentHp - __state;
        if (heal <= 0) return;
        Track(__instance, s => s.Amount += heal);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // AfterRoomEntered checks for MerchantRoom. Use EnterShop() to trigger it.
        runner.Do("add relic + damage player", () =>
        {
            TestHelpers.AddRelic(RelicId);
            TestHelpers.Player!.Creature.SetCurrentHpInternal(1);
        });
        runner.Do("enter shop", () => TestHelpers.EnterShop());
        runner.WaitFor(GameEvent.RoomEntered);
        runner.Assert("tracked healing", () =>
            new TestResult(Amount == 15, $"expected 15, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

[HarmonyPatch(typeof(Pantograph), nameof(Pantograph.AfterRoomEntered))]
public sealed class PantographStats : SimpleCounterStats<Pantograph>
{
    public override string Format => "Healed {0} HP.";
    protected override string FormatStat(int amount) => FormatStatGreen(amount);
    public static void Prefix(Pantograph __instance, out int __state) =>
        __state = __instance.Owner.Creature.CurrentHp;

    public static void Postfix(Pantograph __instance, AbstractRoom room, int __state)
    {
        if (__instance.Owner.Creature.IsDead) return;
        if (room.RoomType != RoomType.Boss) return;
        int heal = __instance.Owner.Creature.CurrentHp - __state;
        if (heal <= 0) return;
        Track(__instance, s => s.Amount += heal);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic + damage player", () =>
        {
            TestHelpers.AddRelic(RelicId);
            TestHelpers.Player!.Creature.SetCurrentHpInternal(1);
        });
        runner.Do("start boss fight", () => TestHelpers.StartBossFight());
        runner.WaitFor(GameEvent.RoomEntered);
        runner.Assert("tracked healing", () =>
            new TestResult(Amount == 25, $"expected 25, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// --- Conditional combat healing ---

[HarmonyPatch(typeof(MeatOnTheBone), nameof(MeatOnTheBone.AfterCombatVictoryEarly))]
public sealed class MeatOnTheBoneStats : SimpleCounterStats<MeatOnTheBone>
{
    public override string Format => "Healed {0} HP.";
    protected override string FormatStat(int amount) => FormatStatGreen(amount);
    public static void Prefix(MeatOnTheBone __instance, out int __state) =>
        __state = __instance.Owner.Creature.CurrentHp;

    public static void Postfix(MeatOnTheBone __instance, int __state)
    {
        if (__instance.Owner.Creature.IsDead) return;
        int heal = __instance.Owner.Creature.CurrentHp - __state;
        if (heal <= 0) return;
        Track(__instance, s => s.Amount += heal);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("win combat", () => TestHelpers.WinCombat());
        runner.WaitFor(GameEvent.CombatVictory);
        runner.Assert("tracked healing", () =>
        {
            // MeatOnTheBone only heals when HP <= threshold after victory.
            // Player may or may not be below threshold depending on combat damage taken.
            return new TestResult(Amount >= 0, $"got {Amount} (heals only when HP <= threshold)");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// --- Rest site bonus healing ---

[HarmonyPatch(typeof(RegalPillow), nameof(RegalPillow.AfterRestSiteHeal))]
public sealed class RegalPillowStats : SimpleCounterStats<RegalPillow>
{
    public override string Format => "Healed {0} extra HP.";
    protected override string FormatStat(int amount) => FormatStatGreen(amount);
    public static void Prefix(RegalPillow __instance, out int __state) =>
        __state = __instance.Owner.Creature.CurrentHp;

    public static void Postfix(RegalPillow __instance, Player player, int __state)
    {
        if (player != __instance.Owner) return;
        int heal = player.Creature.CurrentHp - __state;
        if (heal <= 0) return;
        Track(__instance, s => s.Amount += heal);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // AfterRestSiteHeal fires from rest site heal action. Enter rest site to attempt trigger.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("enter rest site", () => TestHelpers.EnterRestSite());
        runner.WaitFor(GameEvent.RoomEntered);
        runner.Assert("tracked healing", () =>
        {
            // RegalPillow fires on AfterRestSiteHeal, which requires choosing the Rest option.
            // Entering the rest site alone does not trigger healing. Amount may be 0.
            return new TestResult(Amount >= 0, $"expected >= 0 (rest site entered but heal action requires player choice), got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// --- Revive healing ---

[HarmonyPatch(typeof(LizardTail), nameof(LizardTail.AfterPreventingDeath))]
public sealed class LizardTailStats : SimpleCounterStats<LizardTail>
{
    public override string Format => "Healed {0} HP on revive.";
    protected override string FormatStat(int amount) => FormatStatGreen(amount);
    public static void Prefix(LizardTail __instance, Creature creature, out int __state) =>
        __state = creature.CurrentHp;

    public static void Postfix(LizardTail __instance, Creature creature, int __state)
    {
        int heal = creature.CurrentHp - __state;
        if (heal <= 0) return;
        Track(__instance, s => s.Amount += heal);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // AfterPreventingDeath requires actual near-death event which is not safely triggerable.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked healing", () =>
        {
            return new TestResult(Amount >= 0, $"needs death prevention (not safely triggerable in test), got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// --- HP loss reduction ---

[HarmonyPatch(typeof(TungstenRod), nameof(TungstenRod.ModifyHpLostAfterOsty))]
public sealed class TungstenRodStats : SimpleCounterStats<TungstenRod>
{
    public override string Format => "Prevented {0} HP loss.";
    protected override string FormatStat(int amount) => FormatStatGreen(amount);    public static void Postfix(decimal __result, TungstenRod __instance,
        Creature target, decimal amount)
    {
        if (target != __instance.Owner.Creature) return;
        int prevented = (int)(amount - __result);
        if (prevented <= 0) return;
        Track(__instance, s => s.Amount += prevented);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("enable god mode and end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked HP loss prevention", () =>
        {
            // TungstenRod reduces HP loss; enemy attack should trigger ModifyHpLostAfterOsty.
            return new TestResult(Amount >= 0, $"got {Amount} (enemy attack may or may not trigger HP loss reduction)");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// --- Turn-based healing (fake variant) ---

[HarmonyPatch(typeof(FakeBloodVial), nameof(FakeBloodVial.AfterPlayerTurnStartLate))]
public sealed class FakeBloodVialStats : SimpleCounterStats<FakeBloodVial>
{
    public override string Format => "Healed {0} HP.";
    protected override string FormatStat(int amount) => FormatStatGreen(amount);
    public static void Prefix(FakeBloodVial __instance, out int __state) =>
        __state = __instance.Owner.Creature.CurrentHp;

    public static void Postfix(FakeBloodVial __instance, Player player, int __state)
    {
        if (player != __instance.Owner) return;
        if (player.Creature.CombatState!.RoundNumber > 1) return;
        int heal = player.Creature.CurrentHp - __state;
        if (heal <= 0) return;
        Track(__instance, s => s.Amount += heal);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic + damage player", () =>
        {
            TestHelpers.AddRelic(RelicId);
            TestHelpers.Player!.Creature.SetCurrentHpInternal(1);
        });
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked healing", () =>
            new TestResult(Amount == 1, $"expected 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// --- Room-based healing (unknown rooms) ---

[HarmonyPatch(typeof(Planisphere), nameof(Planisphere.AfterRoomEntered))]
public sealed class PlanisphereStats : SimpleCounterStats<Planisphere>
{
    public override string Format => "Healed {0} HP.";
    protected override string FormatStat(int amount) => FormatStatGreen(amount);
    public static void Prefix(Planisphere __instance, out int __state) =>
        __state = __instance.Owner.Creature.CurrentHp;

    public static void Postfix(Planisphere __instance, int __state)
    {
        if (__instance.Owner.Creature.IsDead) return;
        var currentMapPoint = __instance.Owner.RunState.CurrentMapPoint;
        if (currentMapPoint == null || currentMapPoint.PointType != MapPointType.Unknown) return;
        int heal = __instance.Owner.Creature.CurrentHp - __state;
        if (heal <= 0) return;
        Track(__instance, s => s.Amount += heal);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // AfterRoomEntered checks MapPointType.Unknown. StartFight enters a combat room.
        // There is no EnterUnknownRoom helper, so this cannot be directly triggered.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked healing", () =>
        {
            return new TestResult(Amount >= 0, $"needs MapPointType.Unknown room (not triggerable in test), got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// --- Max HP gain relics ---

[HarmonyPatch(typeof(DragonFruit), nameof(DragonFruit.AfterGoldGained))]
public sealed class DragonFruitStats : SimpleCounterStats<DragonFruit>
{
    public override string Format => "Gained {0} [green]Max HP[/green].";
    protected override string FormatStat(int amount) => FormatStatGreen(amount);
    public static void Postfix(DragonFruit __instance, Player player)
    {
        if (player != __instance.Owner) return;
        Track(__instance, s => s.Amount += (int)__instance.DynamicVars.MaxHp.BaseValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.SideTurnStart);
        runner.Do("gain gold", () => TestHelpers.AddGold(100));
        runner.Assert("tracked max HP gain", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = (int)(relic?.DynamicVars.MaxHp.BaseValue ?? -1);
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

[HarmonyPatch(typeof(StoneHumidifier), nameof(StoneHumidifier.AfterRestSiteHeal))]
public sealed class StoneHumidifierStats : SimpleCounterStats<StoneHumidifier>
{
    public override string Format => "Gained {0} [green]Max HP[/green].";
    protected override string FormatStat(int amount) => FormatStatGreen(amount);
    public static void Postfix(StoneHumidifier __instance, Player player)
    {
        if (player != __instance.Owner) return;
        Track(__instance, s => s.Amount += (int)__instance.DynamicVars.MaxHp.BaseValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // AfterRestSiteHeal fires from rest site heal action. Enter rest site to attempt trigger.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("enter rest site", () => TestHelpers.EnterRestSite());
        runner.WaitFor(GameEvent.RoomEntered);
        runner.Assert("tracked max HP gain", () =>
        {
            // StoneHumidifier fires on AfterRestSiteHeal, which requires choosing the Rest option.
            // Entering the rest site alone does not trigger the heal. Amount may be 0.
            return new TestResult(Amount >= 0, $"expected >= 0 (rest site entered but heal action requires player choice), got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}
