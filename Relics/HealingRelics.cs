using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using RelicStats.Core;

namespace RelicStats.Relics;

// --- Heal after combat ---

[HarmonyPatch(typeof(BurningBlood), nameof(BurningBlood.AfterCombatVictory))]
public sealed class BurningBloodStats : SimpleCounterStats<BurningBlood>
{
    public override string Format => "Healed {0} HP.";
    protected override string FormatStat(int amount) => FormatStatGreen(amount);    public static void Postfix(BurningBlood __instance)
    {
        if (__instance.Owner.Creature.IsDead) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Heal.IntValue);
    }
}

[HarmonyPatch(typeof(BlackBlood), nameof(BlackBlood.AfterCombatVictory))]
public sealed class BlackBloodStats : SimpleCounterStats<BlackBlood>
{
    public override string Format => "Healed {0} HP.";
    protected override string FormatStat(int amount) => FormatStatGreen(amount);    public static void Postfix(BlackBlood __instance)
    {
        if (__instance.Owner.Creature.IsDead) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Heal.IntValue);
    }
}

// --- Turn-based healing ---

[HarmonyPatch(typeof(BloodVial), nameof(BloodVial.AfterPlayerTurnStartLate))]
public sealed class BloodVialStats : SimpleCounterStats<BloodVial>
{
    public override string Format => "Healed {0} HP.";
    protected override string FormatStat(int amount) => FormatStatGreen(amount);    public static void Postfix(BloodVial __instance, Player player)
    {
        if (player != __instance.Owner) return;
        if (player.Creature.CombatState.RoundNumber > 1) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Heal.IntValue);
    }
}

// --- Doom healing ---

[HarmonyPatch(typeof(BookRepairKnife), nameof(BookRepairKnife.AfterDiedToDoom))]
public sealed class BookRepairKnifeStats : SimpleCounterStats<BookRepairKnife>
{
    public override string Format => "Healed {0} HP.";
    protected override string FormatStat(int amount) => FormatStatGreen(amount);    public static void Postfix(BookRepairKnife __instance, IReadOnlyList<Creature> creatures)
    {
        int num = creatures.Count(c =>
            c != __instance.Owner.Creature &&
            c.Powers.All(p => p.ShouldOwnerDeathTriggerFatal()));
        if (num == 0) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Heal.IntValue * num);
    }
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
        __state = (bool)TriggeredField.GetValue(__instance);

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
}

// --- Room-based healing ---

[HarmonyPatch(typeof(EternalFeather), nameof(EternalFeather.AfterRoomEntered))]
public sealed class EternalFeatherStats : SimpleCounterStats<EternalFeather>
{
    public override string Format => "Healed {0} HP.";
    protected override string FormatStat(int amount) => FormatStatGreen(amount);    public static void Postfix(EternalFeather __instance, AbstractRoom room)
    {
        if (room is not RestSiteRoom) return;
        int deckCount = PileType.Deck.GetPile(__instance.Owner).Cards.Count;
        int stacks = deckCount / __instance.DynamicVars.Cards.IntValue;
        int heal = __instance.DynamicVars.Heal.IntValue * stacks;
        Track(__instance, s => s.Amount += heal);
    }
}

[HarmonyPatch(typeof(MealTicket), nameof(MealTicket.AfterRoomEntered))]
public sealed class MealTicketStats : SimpleCounterStats<MealTicket>
{
    public override string Format => "Healed {0} HP.";
    protected override string FormatStat(int amount) => FormatStatGreen(amount);    public static void Postfix(MealTicket __instance, AbstractRoom room)
    {
        if (__instance.Owner.Creature.IsDead) return;
        if (room is not MerchantRoom) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Heal.IntValue);
    }
}

[HarmonyPatch(typeof(Pantograph), nameof(Pantograph.AfterRoomEntered))]
public sealed class PantographStats : SimpleCounterStats<Pantograph>
{
    public override string Format => "Healed {0} HP.";
    protected override string FormatStat(int amount) => FormatStatGreen(amount);    public static void Postfix(Pantograph __instance, AbstractRoom room)
    {
        if (__instance.Owner.Creature.IsDead) return;
        if (room.RoomType != RoomType.Boss) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Heal.IntValue);
    }
}

// --- Conditional combat healing ---

[HarmonyPatch(typeof(MeatOnTheBone), nameof(MeatOnTheBone.AfterCombatVictoryEarly))]
public sealed class MeatOnTheBoneStats : SimpleCounterStats<MeatOnTheBone>
{
    public override string Format => "Healed {0} HP.";
    protected override string FormatStat(int amount) => FormatStatGreen(amount);    public static void Postfix(MeatOnTheBone __instance)
    {
        if (__instance.Owner.Creature.IsDead) return;
        // Mirror the relic's own condition: heal only when HP <= threshold
        var creature = __instance.Owner.Creature;
        int threshold = (int)((decimal)creature.MaxHp *
            (__instance.DynamicVars["HpThreshold"].BaseValue / 100m));
        if (creature.CurrentHp > threshold) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Heal.IntValue);
    }
}

// --- Rest site bonus healing ---

[HarmonyPatch(typeof(RegalPillow), nameof(RegalPillow.AfterRestSiteHeal))]
public sealed class RegalPillowStats : SimpleCounterStats<RegalPillow>
{
    public override string Format => "Healed {0} extra HP.";
    protected override string FormatStat(int amount) => FormatStatGreen(amount);    public static void Postfix(RegalPillow __instance, Player player)
    {
        if (player != __instance.Owner) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Heal.IntValue);
    }
}

// --- Revive healing ---

[HarmonyPatch(typeof(LizardTail), nameof(LizardTail.AfterPreventingDeath))]
public sealed class LizardTailStats : SimpleCounterStats<LizardTail>
{
    public override string Format => "Healed {0} HP on revive.";
    protected override string FormatStat(int amount) => FormatStatGreen(amount);    public static void Postfix(LizardTail __instance, Creature creature)
    {
        int heal = (int)((decimal)creature.MaxHp *
            (__instance.DynamicVars.Heal.BaseValue / 100m));
        Track(__instance, s => s.Amount += heal);
    }
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
}

// --- Turn-based healing (fake variant) ---

[HarmonyPatch(typeof(FakeBloodVial), nameof(FakeBloodVial.AfterPlayerTurnStartLate))]
public sealed class FakeBloodVialStats : SimpleCounterStats<FakeBloodVial>
{
    public override string Format => "Healed {0} HP.";
    protected override string FormatStat(int amount) => FormatStatGreen(amount);
    public static void Postfix(FakeBloodVial __instance, Player player)
    {
        if (player != __instance.Owner) return;
        if (player.Creature.CombatState.RoundNumber > 1) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Heal.IntValue);
    }
}

// --- Room-based healing (unknown rooms) ---

[HarmonyPatch(typeof(Planisphere), nameof(Planisphere.AfterRoomEntered))]
public sealed class PlanisphereStats : SimpleCounterStats<Planisphere>
{
    public override string Format => "Healed {0} HP.";
    protected override string FormatStat(int amount) => FormatStatGreen(amount);
    public static void Postfix(Planisphere __instance)
    {
        if (__instance.Owner.Creature.IsDead) return;
        var currentMapPoint = __instance.Owner.RunState.CurrentMapPoint;
        if (currentMapPoint == null || currentMapPoint.PointType != MapPointType.Unknown) return;
        Track(__instance, s => s.Amount += (int)__instance.DynamicVars.Heal.BaseValue);
    }
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
}
