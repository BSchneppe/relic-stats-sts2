using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
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
    public override StatCadence Cadence => StatCadence.Combat;
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
    public override StatCadence Cadence => StatCadence.Combat;
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
    public override StatCadence Cadence => StatCadence.Combat;
    protected override string FormatStat(int amount) => FormatStatGreen(amount);
    public static void Prefix(BloodVial __instance, Player player, out int __state) =>
        __state = __instance.Owner.Creature.CurrentHp;

    public static void Postfix(BloodVial __instance, Player player, int __state)
    {
        if (player != __instance.Owner) return;
        if (player.PlayerCombatState!.TurnNumber > 1) return;
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
        runner.Assert("tracked healing on turn 1", () =>
            new TestResult(Amount == 2, $"expected 2, got {Amount}"));
        // Turn 1 only: turn 2's AfterPlayerTurnStartLate has run by the next PlayerTurnStart and must not heal.
        runner.Do("god mode + protect enemy + end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("no heal on turn 2", () =>
            new TestResult(Amount == 2, $"expected still 2, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
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
        // Doom kills its owner at the end of the owner's side turn once HP <= Doom
        // (DoomPower.BeforeSideTurnEnd -> DoomKill -> Hook.AfterDiedToDoom -> the knife's heal).
        // Two enemies, only one doomed: killing the last enemy would end the combat inside DoomKill,
        // before AfterDiedToDoom runs, and the heal would never happen. The survivor keeps the combat
        // going. No god mode (it heals and would hide whether the knife did); 999 block instead keeps
        // the enemy attacks off the player's HP so the only HP change is the knife's heal.
        int startHp = 0;
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start a two-enemy fight", () => TestHelpers.StartFight("TOADPOLES_WEAK"));
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("doom one enemy + end turn", () =>
        {
            var player = TestHelpers.Player!;
            TestHelpers.SetPlayerHp(player.Creature.MaxHp - 20);
            startHp = player.Creature.CurrentHp;
            TestHelpers.GiveBlock(999);
            var state = player.Creature.CombatState!;
            var enemies = state.HittableEnemies.ToList();
            if (enemies.Count < 2) throw new InvalidOperationException($"expected 2 enemies, got {enemies.Count}");
            int index = state.Creatures.ToList().IndexOf(enemies[0]);
            TestHelpers.ApplyPower("DOOM_POWER", 999, index);
            TestHelpers.EndTurn();
        });
        runner.WaitUntil("the knife healed", () => Amount > 0, 15000);
        runner.Assert("tracked one doomed enemy's heal", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var heal = (int)(relic?.DynamicVars.Heal.BaseValue ?? -1);
            var healed = TestHelpers.Player!.Creature.CurrentHp - startHp;
            var alive = TestHelpers.Player!.Creature.CombatState?.HittableEnemies.Count ?? -1;
            return new TestResult(heal > 0 && Amount == heal && healed == heal && alive == 1,
                $"expected {heal} (one doomed enemy), got {Amount}; HP rose by {healed}; {alive} enemy left");
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
        // Heals unblocked damage taken during the player's OWN turn, once per turn. No god mode: the hit
        // must really land.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("hit the player for 5 on their turn", () => TestHelpers.DealDamageToPlayer(5));
        runner.WaitFor(GameEvent.DamageReceived);
        runner.Assert("tracked the heal", () => new TestResult(Amount == 5, $"expected 5, got {Amount}"));
        runner.Do("hit again for 3 in the same turn", () => TestHelpers.DealDamageToPlayer(3));
        runner.WaitFor(GameEvent.DamageReceived);
        runner.Assert("once per turn", () => new TestResult(Amount == 5, $"expected still 5, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// --- Room-based healing ---

[HarmonyPatch(typeof(EternalFeather), nameof(EternalFeather.AfterRoomEntered))]
public sealed class EternalFeatherStats : SimpleCounterStats<EternalFeather>
{
    public override string Format => "Healed {0} HP.";
    public override StatCadence Cadence => StatCadence.Total;
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
        // Heals Heal per Cards in the permanent deck on entering a rest site. The harness clears the deck
        // before each test, so pad it with 10 Strikes through the real card pipeline (needs a combat
        // room for the add tween; each add signals CardChangedPiles) -> 2 stacks of 3.
        const int padded = 10;
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        for (int i = 1; i <= padded; i++)
        {
            runner.Do($"add strike {i} to deck", () => TestHelpers.AddCardToDeck("STRIKE_IRONCLAD"));
            runner.WaitFor(GameEvent.CardChangedPiles, 8000);
        }
        runner.Do("damage player + enter rest site", () => { TestHelpers.SetPlayerHp(1); TestHelpers.EnterRestSite(); });
        runner.WaitFor(GameEvent.RoomEntered, 8000);
        int Expected(out int deck)
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var heal = (int)(relic?.DynamicVars.Heal.BaseValue ?? -1);
            var cards = relic?.DynamicVars.Cards.IntValue ?? -1;
            deck = TestHelpers.Player!.Deck.Cards.Count;
            return heal > 0 && cards > 0 ? heal * (deck / cards) : -1;
        }
        runner.Assert("tracked healing per deck stack", () =>
        {
            var expected = Expected(out int deck);
            return new TestResult(deck == padded && expected > 0 && Amount == expected, $"expected {expected} with {padded} cards (deck has {deck}), got {Amount}");
        });
        // Rest sites only: a shop must not heal.
        runner.Do("enter shop", () => TestHelpers.EnterShop());
        runner.WaitFor(GameEvent.RoomEntered, 8000);
        runner.Assert("no heal in a shop", () =>
        {
            var expected = Expected(out _);
            return new TestResult(Amount == expected, $"expected still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

[HarmonyPatch(typeof(MealTicket), nameof(MealTicket.AfterRoomEntered))]
public sealed class MealTicketStats : SimpleCounterStats<MealTicket>
{
    public override string Format => "Healed {0} HP.";
    public override StatCadence Cadence => StatCadence.Total;
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
        // Shops only: a rest site must not heal.
        runner.Do("enter rest site", () => TestHelpers.EnterRestSite());
        runner.WaitFor(GameEvent.RoomEntered, 8000);
        runner.Assert("no heal at a rest site", () =>
            new TestResult(Amount == 15, $"expected still 15, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// Pantograph: heals at the start of Boss combats
[HarmonyPatch(typeof(Pantograph), nameof(Pantograph.BeforeCombatStart))]
public sealed class PantographStats : SimpleCounterStats<Pantograph>
{
    public override string Format => "Healed {0} HP.";
    public override StatCadence Cadence => StatCadence.Combat;
    protected override string FormatStat(int amount) => FormatStatGreen(amount);

    // The heal is awaited inside BeforeCombatStart, so a Postfix can't observe the HP change
    // reliably. Mirror the relic's condition and compute the effective heal from missing HP.
    public static void Prefix(Pantograph __instance)
    {
        var creature = __instance.Owner.Creature;
        if (creature.IsDead) return;
        if (__instance.Owner.RunState.CurrentRoom?.RoomType != RoomType.Boss) return;
        int heal = (int)Math.Min(__instance.DynamicVars.Heal.BaseValue, creature.MaxHp - creature.CurrentHp);
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
        // BeforeCombatStart fires before the CombatStart event; wait for CombatStart.
        // Boss-room guard: a Monster fight at 1 HP heals nothing.
        runner.Do("start monster fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.CombatStart);
        runner.Assert("no heal in a monster fight", () =>
            new TestResult(Amount == 0, $"expected 0, got {Amount}"));
        runner.Do("start boss fight", () => TestHelpers.StartBossFight());
        runner.WaitFor(GameEvent.CombatStart);
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
    public override StatCadence Cadence => StatCadence.Combat;
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
        // Heals Heal at AfterCombatVictoryEarly when HP <= HpThreshold percent of max. Start under it.
        runner.Do("add relic + damage player", () =>
        {
            TestHelpers.AddRelic(RelicId);
            TestHelpers.SetPlayerHp(1);
        });
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("win combat", () => TestHelpers.WinCombat());
        runner.WaitFor(GameEvent.CombatVictory);
        runner.Assert("tracked healing under the threshold", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = (int)(relic?.DynamicVars.Heal.BaseValue ?? -1);
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        // No cheap negative: a full-HP victory needs a second combat.
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// --- Rest site bonus healing ---

// The +15 comes from ModifyRestSiteHealAmount, which runs before the heal; AfterRestSiteHeal only
// flashes the relic, so an HP delta across it is always 0. ModifyRestSiteHealAmount is also evaluated
// for the Rest option's preview text, so the count is scoped to HealRestSiteOption.ExecuteRestSiteHeal,
// which computes the real amount synchronously before its first await.
[HarmonyPatch]
public sealed class RegalPillowStats : SimpleCounterStats<RegalPillow>
{
    [ThreadStatic] private static bool _inRestHeal;

    public override string Format => "Healed {0} extra HP.";
    public override StatCadence Cadence => StatCadence.Total;
    protected override string FormatStat(int amount) => FormatStatGreen(amount);

    [HarmonyPatch(typeof(HealRestSiteOption), nameof(HealRestSiteOption.ExecuteRestSiteHeal))]
    [HarmonyPrefix]
    public static void ExecuteRestSiteHealPrefix() => _inRestHeal = true;

    [HarmonyPatch(typeof(HealRestSiteOption), nameof(HealRestSiteOption.ExecuteRestSiteHeal))]
    [HarmonyPostfix]
    public static void ExecuteRestSiteHealPostfix() => _inRestHeal = false;

    [HarmonyPatch(typeof(RegalPillow), nameof(RegalPillow.ModifyRestSiteHealAmount))]
    [HarmonyPostfix]
    public static void ModifyRestSiteHealAmountPostfix(RegalPillow __instance, Creature creature, decimal amount, decimal __result)
    {
        if (!_inRestHeal) return;
        if (creature.Player != __instance.Owner) return;
        int extra = (int)(__result - amount);
        // Report what the bonus actually healed: only the part that fits under max HP after the base heal.
        int room = creature.MaxHp - creature.CurrentHp - (int)amount;
        extra = Math.Min(extra, Math.Max(room, 0));
        if (extra <= 0) return;
        // ExecuteRestSiteHeal evaluates the heal amount once for the heal, but the heal refreshes the
        // rest-site UI, which re-reads it inside the same scope. Count the first evaluation only.
        _inRestHeal = false;
        Track(__instance, s => s.Amount += extra);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic + damage player", () =>
        {
            TestHelpers.AddRelic(RelicId);
            TestHelpers.SetPlayerHp(1);
        });
        runner.Do("enter rest site", () => TestHelpers.EnterRestSite());
        runner.WaitFor(GameEvent.RoomEntered, 8000);
        // Entering only offers the options (and renders their previews): nothing yet.
        runner.Assert("nothing from entering alone", () => new TestResult(Amount == 0, $"expected 0, got {Amount}"));
        runner.Do("rest", () => TestHelpers.RestAtSite());
        runner.Assert("tracked the extra rest heal", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = (int)(relic?.DynamicVars.Heal.BaseValue ?? -1);
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.CloseOverlays(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// --- Revive healing ---

[HarmonyPatch(typeof(LizardTail), nameof(LizardTail.AfterPreventingDeath))]
public sealed class LizardTailStats : SimpleCounterStats<LizardTail>
{
    public override string Format => "Healed {0} HP on revive.";
    public override StatCadence Cadence => StatCadence.Total;
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
        // No god mode: the lethal hit must really land. A non-lethal hit goes through AfterDamageReceived
        // and never reaches the death check, so it must not count.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("non-lethal hit", () => TestHelpers.DealDamageToPlayer(1));
        runner.WaitFor(GameEvent.DamageReceived);
        runner.Assert("nothing from a survivable hit", () => new TestResult(Amount == 0, $"expected 0, got {Amount}"));
        // A lethal hit skips AfterDamageReceived (CreatureCmd.Damage only fires it for survivors) and goes
        // to Kill, where ShouldDieLate == false routes to Hook.AfterDeath(prevented) and then
        // AfterPreventingDeath, which heals max(1, MaxHp * Heal%) from 0. Sync on Death, then span a turn
        // under god mode (Buffer keeps the enemy from moving HP) so the heal has landed before asserting.
        runner.Do("lethal hit", () => TestHelpers.DealDamageToPlayer(9999));
        runner.WaitFor(GameEvent.Death, 15000);
        runner.Do("god mode + protect enemy + end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked the revive heal", () =>
        {
            var creature = TestHelpers.Player!.Creature;
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var percent = relic?.DynamicVars.Heal.BaseValue ?? -1;
            int expected = (int)Math.Max(1m, creature.MaxHp * (percent / 100m));
            return new TestResult(percent > 0 && Amount == expected && creature.CurrentHp == expected,
                $"expected {expected} (player at {creature.CurrentHp}/{creature.MaxHp}), got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// --- HP loss reduction ---

[HarmonyPatch(typeof(TungstenRod), nameof(TungstenRod.ModifyHpLostAfterOsty))]
public sealed class TungstenRodStats : SimpleCounterStats<TungstenRod>
{
    public override string Format => "Prevented {0} HP loss.";
    protected override string FormatStat(int amount) => FormatStatGreen(amount);
    public static void Postfix(decimal __result, TungstenRod __instance,
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
        // No god mode: the hit must really cost HP.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        int Expected() => (int)(TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId)?.DynamicVars["HpLossReduction"].BaseValue ?? -1);
        runner.Do("hit the player for 5", () => TestHelpers.DealDamageToPlayer(5));
        runner.WaitFor(GameEvent.DamageReceived);
        runner.Assert("tracked the reduced HP loss", () =>
        {
            var expected = Expected();
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        // Hook.ModifyHpLost receives max(damage - blocked, 0): a fully blocked hit passes 0 and there is
        // nothing to reduce.
        runner.Do("block 10, hit for 5", () => { TestHelpers.GiveBlock(10); TestHelpers.DealDamageToPlayer(5); });
        runner.WaitFor(GameEvent.DamageReceived);
        runner.Assert("nothing on a blocked hit", () =>
        {
            var expected = Expected();
            return new TestResult(Amount == expected, $"expected still {expected}, got {Amount}");
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
    public override StatCadence Cadence => StatCadence.Combat;
    protected override string FormatStat(int amount) => FormatStatGreen(amount);
    public static void Prefix(FakeBloodVial __instance, out int __state) =>
        __state = __instance.Owner.Creature.CurrentHp;

    public static void Postfix(FakeBloodVial __instance, Player player, int __state)
    {
        if (player != __instance.Owner) return;
        if (player.PlayerCombatState!.TurnNumber > 1) return;
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
        runner.Assert("tracked healing on turn 1", () =>
            new TestResult(Amount == 1, $"expected 1, got {Amount}"));
        // Turn 1 only: turn 2's AfterPlayerTurnStartLate has run by the next PlayerTurnStart and must not heal.
        runner.Do("god mode + protect enemy + end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("no heal on turn 2", () =>
            new TestResult(Amount == 1, $"expected still 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// --- Room-based healing (unknown rooms) ---

[HarmonyPatch(typeof(Planisphere), nameof(Planisphere.AfterRoomEntered))]
public sealed class PlanisphereStats : SimpleCounterStats<Planisphere>
{
    public override string Format => "Healed {0} HP.";
    public override StatCadence Cadence => StatCadence.Total;
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
        // Heals Heal on entering a room while the CURRENT map point is an Unknown ("?") node (and it is
        // the first room of that point). RunManager.EnterRoomDebug leaves a real map's current point
        // alone but a MockSinglePointActMap takes its type from the pointType argument, so both are set:
        // the point itself and the debug entry's pointType. The first entry uses a non-unknown type.
        MapPointType? previousType = null;
        static void EnterDebugRoom(RoomType roomType, MapPointType pointType) =>
            TestHelpers.EnterDebugRoom(roomType, pointType);
        runner.Do("add relic + damage player", () =>
        {
            TestHelpers.AddRelic(RelicId);
            TestHelpers.SetPlayerHp(1);
        });
        runner.Do("enter a rest site on a rest-site point", () =>
        {
            previousType = TestHelpers.SetCurrentMapPointType(MapPointType.RestSite);
            if (previousType == null)
                MainFile.Logger.Warn("[Planisphere test] no current map point; relying on the debug room's pointType");
            EnterDebugRoom(RoomType.RestSite, MapPointType.RestSite);
        });
        runner.WaitFor(GameEvent.RoomEntered, 8000);
        runner.Assert("nothing on a known point", () => new TestResult(Amount == 0, $"expected 0, got {Amount}"));
        runner.Do("enter a shop on an unknown point", () =>
        {
            TestHelpers.SetCurrentMapPointType(MapPointType.Unknown);
            EnterDebugRoom(RoomType.Shop, MapPointType.Unknown);
        });
        runner.WaitFor(GameEvent.RoomEntered, 8000);
        runner.Assert("tracked the unknown-point heal", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = (int)(relic?.DynamicVars.Heal.BaseValue ?? -1);
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() =>
        {
            if (previousType is { } restore) TestHelpers.SetCurrentMapPointType(restore);
            TestHelpers.RemoveRelic(RelicId);
            Reset();
        });
    }
#endif
}

// --- Max HP gain relics ---

[HarmonyPatch(typeof(DragonFruit), nameof(DragonFruit.AfterGoldGained))]
public sealed class DragonFruitStats : SimpleCounterStats<DragonFruit>
{
    public override string Format => "Gained {0} [green]Max HP[/green].";
    public override StatCadence Cadence => StatCadence.Total;
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
    public override StatCadence Cadence => StatCadence.Total;
    protected override string FormatStat(int amount) => FormatStatGreen(amount);
    public static void Postfix(StoneHumidifier __instance, Player player)
    {
        if (player != __instance.Owner) return;
        Track(__instance, s => s.Amount += (int)__instance.DynamicVars.MaxHp.BaseValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // AfterRestSiteHeal runs inside HealRestSiteOption.ExecuteRestSiteHeal, right after the heal
        // (no awaits on the rest-site path). Entering the site alone only offers the options.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("enter rest site", () => TestHelpers.EnterRestSite());
        runner.WaitFor(GameEvent.RoomEntered, 8000);
        runner.Assert("nothing from entering alone", () => new TestResult(Amount == 0, $"expected 0, got {Amount}"));
        runner.Do("rest", () => TestHelpers.RestAtSite());
        runner.Assert("tracked max HP gain", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = (int)(relic?.DynamicVars.MaxHp.BaseValue ?? -1);
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.CloseOverlays(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}
