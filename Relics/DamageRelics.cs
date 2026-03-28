using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;
using RelicStats.Core;

namespace RelicStats.Relics;

// --- Direct damage relics ---

[HarmonyPatch(typeof(CharonsAshes), nameof(CharonsAshes.AfterCardExhausted))]
public sealed class CharonsAshesStats : SimpleCounterStats<CharonsAshes>
{
    public override string Format => "Dealt {0} [gold]Damage[/gold].";
    public static void Postfix(CharonsAshes __instance, CardModel card)
    {
        if (card.Owner != __instance.Owner) return;
        Track(__instance, s => s.Amount +=
            __instance.DynamicVars.Damage.IntValue *
            __instance.Owner.Creature.CombatState.HittableEnemies.Count);
    }
}

[HarmonyPatch(typeof(FestivePopper), nameof(FestivePopper.AfterPlayerTurnStart))]
public sealed class FestivePopperStats : SimpleCounterStats<FestivePopper>
{
    public override string Format => "Dealt {0} [gold]Damage[/gold].";
    public static void Postfix(FestivePopper __instance, Player player)
    {
        if (player != __instance.Owner) return;
        if (player.Creature.CombatState.RoundNumber != 1) return;
        Track(__instance, s => s.Amount +=
            __instance.DynamicVars.Damage.IntValue *
            __instance.Owner.Creature.CombatState.HittableEnemies.Count);
    }
}

[HarmonyPatch(typeof(Kusarigama), nameof(Kusarigama.AfterCardPlayed))]
public sealed class KusarigamaStats : SimpleCounterStats<Kusarigama>
{
    private static readonly FieldInfo AttacksField =
        AccessTools.Field(typeof(Kusarigama), "_attacksPlayedThisTurn");

    public override string Format => "Dealt {0} [gold]Damage[/gold].";
    public static void Postfix(Kusarigama __instance, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != __instance.Owner) return;
        if (!CombatManager.Instance.IsInProgress) return;
        if (cardPlay.Card.Type != CardType.Attack) return;
        var attacks = (int)AttacksField.GetValue(__instance);
        if (attacks % __instance.DynamicVars.Cards.IntValue != 0) return;
        if (!__instance.Owner.Creature.CombatState.HittableEnemies.Any()) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Damage.IntValue);
    }
}

[HarmonyPatch(typeof(LetterOpener), nameof(LetterOpener.AfterCardPlayed))]
public sealed class LetterOpenerStats : SimpleCounterStats<LetterOpener>
{
    private static readonly FieldInfo SkillsField =
        AccessTools.Field(typeof(LetterOpener), "_skillsPlayedThisTurn");

    public override string Format => "Dealt {0} [gold]Damage[/gold].";
    public static void Postfix(LetterOpener __instance, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != __instance.Owner) return;
        if (!CombatManager.Instance.IsInProgress) return;
        if (cardPlay.Card.Type != CardType.Skill) return;
        var skills = (int)SkillsField.GetValue(__instance);
        if (skills % __instance.DynamicVars.Cards.IntValue != 0) return;
        Track(__instance, s => s.Amount +=
            __instance.DynamicVars.Damage.IntValue *
            __instance.Owner.Creature.CombatState.HittableEnemies.Count);
    }
}

[HarmonyPatch(typeof(MercuryHourglass), nameof(MercuryHourglass.AfterPlayerTurnStart))]
public sealed class MercuryHourglassStats : SimpleCounterStats<MercuryHourglass>
{
    public override string Format => "Dealt {0} [gold]Damage[/gold].";
    public static void Postfix(MercuryHourglass __instance, Player player)
    {
        if (player != __instance.Owner) return;
        Track(__instance, s => s.Amount +=
            __instance.DynamicVars.Damage.IntValue *
            __instance.Owner.Creature.CombatState.HittableEnemies.Count);
    }
}

[HarmonyPatch(typeof(MrStruggles), nameof(MrStruggles.AfterPlayerTurnStart))]
public sealed class MrStrugglesStats : SimpleCounterStats<MrStruggles>
{
    public override string Format => "Dealt {0} [gold]Damage[/gold].";
    public static void Postfix(MrStruggles __instance, Player player)
    {
        if (player != __instance.Owner) return;
        var combatState = player.Creature.CombatState;
        Track(__instance, s => s.Amount +=
            combatState.RoundNumber * combatState.HittableEnemies.Count);
    }
}

[HarmonyPatch(typeof(ScreamingFlagon), nameof(ScreamingFlagon.BeforeTurnEnd))]
public sealed class ScreamingFlagonStats : SimpleCounterStats<ScreamingFlagon>
{
    public override string Format => "Dealt {0} [gold]Damage[/gold].";
    public static void Postfix(ScreamingFlagon __instance, CombatSide side)
    {
        if (side != CombatSide.Player) return;
        if (!PileType.Hand.GetPile(__instance.Owner).IsEmpty) return;
        Track(__instance, s => s.Amount +=
            __instance.DynamicVars.Damage.IntValue *
            __instance.Owner.Creature.CombatState.HittableEnemies.Count);
    }
}

[HarmonyPatch(typeof(StoneCalendar), nameof(StoneCalendar.BeforeTurnEnd))]
public sealed class StoneCalendarStats : SimpleCounterStats<StoneCalendar>
{
    public override string Format => "Dealt {0} [gold]Damage[/gold].";
    public static void Postfix(StoneCalendar __instance, CombatSide side)
    {
        if (side != __instance.Owner.Creature.Side) return;
        var combatState = __instance.Owner.Creature.CombatState;
        if (combatState.RoundNumber != __instance.DynamicVars["DamageTurn"].IntValue) return;
        Track(__instance, s => s.Amount +=
            __instance.DynamicVars.Damage.IntValue *
            combatState.HittableEnemies.Count);
    }
}

[HarmonyPatch(typeof(Tingsha), nameof(Tingsha.AfterCardDiscarded))]
public sealed class TingshaStats : SimpleCounterStats<Tingsha>
{
    public override string Format => "Dealt {0} [gold]Damage[/gold].";
    public static void Postfix(Tingsha __instance, CardModel card)
    {
        if (card.Owner != __instance.Owner) return;
        if (__instance.Owner.Creature.Side != __instance.Owner.Creature.CombatState.CurrentSide) return;
        if (!__instance.Owner.Creature.CombatState.HittableEnemies.Any()) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Damage.IntValue);
    }
}

// --- ModifyDamageAdditive relics ---
// These are called per-target, so we deduplicate by cardSource to avoid multi-counting.

[HarmonyPatch(typeof(FakeStrikeDummy), nameof(FakeStrikeDummy.ModifyDamageAdditive))]
public sealed class FakeStrikeDummyStats : SimpleCounterStats<FakeStrikeDummy>
{
    public override string Format => "Added {0} [gold]Damage[/gold] to Strikes.";
    [System.ThreadStatic] private static CardModel? _lastCard;
    public static void Postfix(decimal __result, FakeStrikeDummy __instance, CardModel? cardSource)
    {
        if (__result == 0m || cardSource == null || cardSource == _lastCard) return;
        _lastCard = cardSource;
        Track(__instance, s => s.Amount += (int)__result);
    }
}

[HarmonyPatch(typeof(StrikeDummy), nameof(StrikeDummy.ModifyDamageAdditive))]
public sealed class StrikeDummyStats : SimpleCounterStats<StrikeDummy>
{
    public override string Format => "Added {0} [gold]Damage[/gold] to Strikes.";
    [System.ThreadStatic] private static CardModel? _lastCard;
    public static void Postfix(decimal __result, StrikeDummy __instance, CardModel? cardSource)
    {
        if (__result == 0m || cardSource == null || cardSource == _lastCard) return;
        _lastCard = cardSource;
        Track(__instance, s => s.Amount += (int)__result);
    }
}

[HarmonyPatch(typeof(MiniatureCannon), nameof(MiniatureCannon.ModifyDamageAdditive))]
public sealed class MiniatureCannonStats : SimpleCounterStats<MiniatureCannon>
{
    public override string Format => "Added {0} [gold]Damage[/gold] to upgraded attacks.";
    [System.ThreadStatic] private static CardModel? _lastCard;
    public static void Postfix(decimal __result, MiniatureCannon __instance, CardModel? cardSource)
    {
        if (__result == 0m || cardSource == null || cardSource == _lastCard) return;
        _lastCard = cardSource;
        Track(__instance, s => s.Amount += (int)__result);
    }
}

[HarmonyPatch(typeof(MysticLighter), nameof(MysticLighter.ModifyDamageAdditive))]
public sealed class MysticLighterStats : SimpleCounterStats<MysticLighter>
{
    public override string Format => "Added {0} [gold]Damage[/gold] to enchanted attacks.";
    [System.ThreadStatic] private static CardModel? _lastCard;
    public static void Postfix(decimal __result, MysticLighter __instance, CardModel? cardSource)
    {
        if (__result == 0m || cardSource == null || cardSource == _lastCard) return;
        _lastCard = cardSource;
        Track(__instance, s => s.Amount += (int)__result);
    }
}

// --- Additional damage relics ---

// ForgottenSoul: deals damage to a random enemy on exhaust
[HarmonyPatch(typeof(ForgottenSoul), nameof(ForgottenSoul.AfterCardExhausted))]
public sealed class ForgottenSoulStats : SimpleCounterStats<ForgottenSoul>
{
    public override string Format => "Dealt {0} [gold]Damage[/gold].";
    public static void Postfix(ForgottenSoul __instance, CardModel card)
    {
        if (card.Owner != __instance.Owner) return;
        if (!__instance.Owner.Creature.CombatState.HittableEnemies.Any()) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Damage.IntValue);
    }
}

// LostWisp: deals damage to all enemies when a Power is played
[HarmonyPatch(typeof(LostWisp), nameof(LostWisp.AfterCardPlayed))]
public sealed class LostWispStats : SimpleCounterStats<LostWisp>
{
    public override string Format => "Dealt {0} [gold]Damage[/gold].";
    public static void Postfix(LostWisp __instance, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != __instance.Owner) return;
        if (!CombatManager.Instance.IsInProgress) return;
        if (cardPlay.Card.Type != CardType.Power) return;
        Track(__instance, s => s.Amount +=
            __instance.DynamicVars.Damage.IntValue *
            __instance.Owner.Creature.CombatState.HittableEnemies.Count);
    }
}

// ParryingShield: deals damage to a random enemy at turn end if block >= threshold
[HarmonyPatch(typeof(ParryingShield), nameof(ParryingShield.AfterTurnEnd))]
public sealed class ParryingShieldStats : SimpleCounterStats<ParryingShield>
{
    public override string Format => "Dealt {0} [gold]Damage[/gold].";
    public static void Postfix(ParryingShield __instance, CombatSide side)
    {
        if (side != CombatSide.Player) return;
        if ((decimal)__instance.Owner.Creature.Block < __instance.DynamicVars.Block.BaseValue) return;
        if (!__instance.Owner.Creature.CombatState.HittableEnemies.Any()) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Damage.IntValue);
    }
}

// TheBoot: boosts low damage hits to 5
[HarmonyPatch(typeof(TheBoot), nameof(TheBoot.ModifyHpLostBeforeOsty))]
public sealed class TheBootStats : SimpleCounterStats<TheBoot>
{
    public override string Format => "Boosted damage to 5 {0} times.";
    public static void Postfix(decimal __result, TheBoot __instance,
        Creature? dealer, decimal amount, ValueProp props)
    {
        if (dealer != __instance.Owner.Creature) return;
        if (!props.HasFlag(ValueProp.Move) || props.HasFlag(ValueProp.Unpowered)) return;
        if (amount < 1m) return;
        if (amount >= __instance.DynamicVars["DamageMinimum"].BaseValue) return;
        Track(__instance, s => s.Amount++);
    }
}

// ThrowingAxe: doubles first card play each combat
[HarmonyPatch(typeof(ThrowingAxe), nameof(ThrowingAxe.ModifyCardPlayCount))]
public sealed class ThrowingAxeStats : SimpleCounterStats<ThrowingAxe>
{
    public override string Format => "Doubled first card {0} times.";
    public static void Postfix(int __result, ThrowingAxe __instance, CardModel card, int playCount)
    {
        if (__result <= playCount) return;
        if (card.Owner != __instance.Owner) return;
        Track(__instance, s => s.Amount++);
    }
}
