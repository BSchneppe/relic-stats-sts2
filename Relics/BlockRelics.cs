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

namespace RelicStats.Relics;

// Anchor: gains block at combat start
[HarmonyPatch(typeof(Anchor), nameof(Anchor.BeforeCombatStart))]
public sealed class AnchorStats : SimpleCounterStats<Anchor>
{
    public override string Format => "Gained {0} [gold]Block[/gold].";
    public static void Postfix(Anchor __instance) =>
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
}

// FakeAnchor: gains block at combat start (weaker variant)
[HarmonyPatch(typeof(FakeAnchor), nameof(FakeAnchor.BeforeCombatStart))]
public sealed class FakeAnchorStats : SimpleCounterStats<FakeAnchor>
{
    public override string Format => "Gained {0} [gold]Block[/gold].";
    public static void Postfix(FakeAnchor __instance) =>
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
}

// CloakClasp: gains block per card in hand at turn end
[HarmonyPatch(typeof(CloakClasp), nameof(CloakClasp.BeforeTurnEnd))]
public sealed class CloakClaspStats : SimpleCounterStats<CloakClasp>
{
    public override string Format => "Gained {0} [gold]Block[/gold].";
    public static void Postfix(CloakClasp __instance, CombatSide side)
    {
        if (side != __instance.Owner.Creature.Side) return;
        var cards = PileType.Hand.GetPile(__instance.Owner).Cards;
        if (cards.Count == 0) return;
        int block = (int)((decimal)cards.Count * __instance.DynamicVars.Block.BaseValue);
        Track(__instance, s => s.Amount += block);
    }
}

// IntimidatingHelmet: gains block when playing high-cost cards
[HarmonyPatch(typeof(IntimidatingHelmet), nameof(IntimidatingHelmet.BeforeCardPlayed))]
public sealed class IntimidatingHelmetStats : SimpleCounterStats<IntimidatingHelmet>
{
    public override string Format => "Gained {0} [gold]Block[/gold].";
    public static void Postfix(IntimidatingHelmet __instance, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != __instance.Owner) return;
        if (cardPlay.Resources.EnergyValue < __instance.DynamicVars.Energy.IntValue) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
    }
}

// Regalite: gains block when colorless cards enter combat
[HarmonyPatch(typeof(Regalite), nameof(Regalite.AfterCardEnteredCombat))]
public sealed class RegaliteStats : SimpleCounterStats<Regalite>
{
    public override string Format => "Gained {0} [gold]Block[/gold].";
    public static void Postfix(Regalite __instance, CardModel card)
    {
        if (card.Owner != __instance.Owner) return;
        if (!card.VisualCardPool.IsColorless) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
    }
}

// SelfFormingClay: applies block-next-turn power after taking unblocked damage
[HarmonyPatch(typeof(SelfFormingClay), nameof(SelfFormingClay.AfterDamageReceived))]
public sealed class SelfFormingClayStats : SimpleCounterStats<SelfFormingClay>
{
    public override string Format => "Gained {0} [gold]Block[/gold].";
    public static void Postfix(SelfFormingClay __instance, Creature target, DamageResult result)
    {
        if (target != __instance.Owner.Creature) return;
        if (result.UnblockedDamage <= 0) return;
        Track(__instance, s => s.Amount += (int)__instance.DynamicVars["BlockNextTurn"].BaseValue);
    }
}

// BoneFlute: gains block when pet (Osty) attacks
[HarmonyPatch(typeof(BoneFlute), nameof(BoneFlute.AfterAttack))]
public sealed class BoneFluteStats : SimpleCounterStats<BoneFlute>
{
    public override string Format => "Gained {0} [gold]Block[/gold].";
    public static void Postfix(BoneFlute __instance, AttackCommand command)
    {
        if (command.Attacker?.Monster is not MegaCrit.Sts2.Core.Models.Monsters.Osty) return;
        if (command.Attacker.PetOwner?.Creature != __instance.Owner.Creature) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
    }
}

// HornCleat: gains block when block is cleared on turn 2
[HarmonyPatch(typeof(HornCleat), nameof(HornCleat.AfterBlockCleared))]
public sealed class HornCleatStats : SimpleCounterStats<HornCleat>
{
    public override string Format => "Gained {0} [gold]Block[/gold].";
    public static void Postfix(HornCleat __instance, Creature creature)
    {
        if (creature.CombatState.RoundNumber != 2) return;
        if (creature != __instance.Owner.Creature) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
    }
}

// Orichalcum: gains block at turn end if no block
[HarmonyPatch(typeof(Orichalcum), nameof(Orichalcum.BeforeTurnEnd))]
public sealed class OrichalcumStats : SimpleCounterStats<Orichalcum>
{
    public override string Format => "Gained {0} [gold]Block[/gold].";
    public static void Postfix(Orichalcum __instance, CombatSide side)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (__instance.Owner.Creature.Block > 0) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
    }
}

// FakeOrichalcum: gains block at turn end if no block (weaker variant)
[HarmonyPatch(typeof(FakeOrichalcum), nameof(FakeOrichalcum.BeforeTurnEnd))]
public sealed class FakeOrichalcumStats : SimpleCounterStats<FakeOrichalcum>
{
    public override string Format => "Gained {0} [gold]Block[/gold].";
    public static void Postfix(FakeOrichalcum __instance, CombatSide side)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (__instance.Owner.Creature.Block > 0) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
    }
}

// ToughBandages: gains block when cards are discarded
[HarmonyPatch(typeof(ToughBandages), nameof(ToughBandages.AfterCardDiscarded))]
public sealed class ToughBandagesStats : SimpleCounterStats<ToughBandages>
{
    public override string Format => "Gained {0} [gold]Block[/gold].";
    public static void Postfix(ToughBandages __instance, CardModel card)
    {
        if (card.Owner != __instance.Owner) return;
        if (__instance.Owner.Creature.Side != __instance.Owner.Creature.CombatState.CurrentSide) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
    }
}

// Gorget: applies Plating power at combat start
[HarmonyPatch(typeof(Gorget), nameof(Gorget.AfterRoomEntered))]
public sealed class GorgetStats : SimpleCounterStats<Gorget>
{
    public override string Format => "Gained {0} [gold]Plating[/gold].";
    public static void Postfix(Gorget __instance, AbstractRoom room)
    {
        if (room is not CombatRoom) return;
        Track(__instance, s => s.Amount += (int)__instance.DynamicVars["PlatingPower"].BaseValue);
    }
}

// OrnamentalFan: gains block every N attacks played
[HarmonyPatch(typeof(OrnamentalFan), nameof(OrnamentalFan.AfterCardPlayed))]
public sealed class OrnamentalFanStats : SimpleCounterStats<OrnamentalFan>
{
    public override string Format => "Gained {0} [gold]Block[/gold].";
    public static void Postfix(OrnamentalFan __instance, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != __instance.Owner) return;
        if (cardPlay.Card.Type != CardType.Attack) return;
        if (__instance.DisplayAmount != __instance.DynamicVars.Cards.IntValue) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
    }
}

// TheAbacus: gains block on shuffle
[HarmonyPatch(typeof(TheAbacus), nameof(TheAbacus.AfterShuffle))]
public sealed class TheAbacusStats : SimpleCounterStats<TheAbacus>
{
    public override string Format => "Gained {0} [gold]Block[/gold].";
    public static void Postfix(TheAbacus __instance, Player shuffler)
    {
        if (shuffler != __instance.Owner) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
    }
}

// CaptainsWheel: gains block when block is cleared on round 3
[HarmonyPatch(typeof(CaptainsWheel), nameof(CaptainsWheel.AfterBlockCleared))]
public sealed class CaptainsWheelStats : SimpleCounterStats<CaptainsWheel>
{
    public override string Format => "Gained {0} [gold]Block[/gold].";
    public static void Postfix(CaptainsWheel __instance, Creature creature)
    {
        if (creature.CombatState.RoundNumber != 3) return;
        if (creature != __instance.Owner.Creature) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
    }
}

// Sai: gains block each turn
[HarmonyPatch(typeof(Sai), nameof(Sai.AfterSideTurnStart))]
public sealed class SaiStats : SimpleCounterStats<Sai>
{
    public override string Format => "Gained {0} [gold]Block[/gold].";
    public static void Postfix(Sai __instance, CombatSide side)
    {
        if (side != CombatSide.Player) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
    }
}

// DaughterOfTheWind: gains block when attacks are played
[HarmonyPatch(typeof(DaughterOfTheWind), nameof(DaughterOfTheWind.AfterCardPlayed))]
public sealed class DaughterOfTheWindStats : SimpleCounterStats<DaughterOfTheWind>
{
    public override string Format => "Gained {0} [gold]Block[/gold].";
    public static void Postfix(DaughterOfTheWind __instance, CardPlay cardPlay)
    {
        if (cardPlay.Card.Type != CardType.Attack) return;
        if (cardPlay.Card.Owner != __instance.Owner) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
    }
}

// RippleBasin: gains block at turn end if no attacks played
[HarmonyPatch(typeof(RippleBasin), nameof(RippleBasin.BeforeTurnEnd))]
public sealed class RippleBasinStats : SimpleCounterStats<RippleBasin>
{
    public override string Format => "Gained {0} [gold]Block[/gold].";
    public static void Postfix(RippleBasin __instance, CombatSide side)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (CombatManager.Instance.History.CardPlaysFinished.Any(
            (CardPlayFinishedEntry e) =>
                e.HappenedThisTurn(__instance.Owner.Creature.CombatState) &&
                e.CardPlay.Card.Type == CardType.Attack &&
                e.CardPlay.Card.Owner == __instance.Owner)) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
    }
}

// TuningFork: gains block every N skills played
[HarmonyPatch(typeof(TuningFork), nameof(TuningFork.AfterCardPlayed))]
public sealed class TuningForkStats : SimpleCounterStats<TuningFork>
{
    private static readonly FieldInfo IsActivatingField =
        AccessTools.Field(typeof(TuningFork), "_isActivating");

    public override string Format => "Gained {0} [gold]Block[/gold].";
    public static void Postfix(TuningFork __instance, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != __instance.Owner) return;
        if (cardPlay.Card.Type != CardType.Skill) return;
        if (!(bool)IsActivatingField.GetValue(__instance)!) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
    }
}

// Vambrace: doubles first block gained each combat
[HarmonyPatch(typeof(Vambrace), nameof(Vambrace.ModifyBlockMultiplicative))]
public sealed class VambraceStats : SimpleCounterStats<Vambrace>
{
    public override string Format => "Doubled first [gold]Block[/gold] {0} times.";
    public static void Postfix(decimal __result, Vambrace __instance)
    {
        if (__result <= 1m) return;
        Track(__instance, s => s.Amount++);
    }
}

// Permafrost: gains block from first Power played each combat
[HarmonyPatch(typeof(Permafrost), nameof(Permafrost.AfterCardPlayed))]
public sealed class PermafrostStats : SimpleCounterStats<Permafrost>
{
    private static readonly FieldInfo ActivatedField =
        AccessTools.Field(typeof(Permafrost), "_activatedThisCombat");

    public override string Format => "Gained {0} [gold]Block[/gold].";

    public static void Prefix(Permafrost __instance, out bool __state) =>
        __state = (bool)ActivatedField.GetValue(__instance)!;

    public static void Postfix(Permafrost __instance, bool __state, CardPlay cardPlay)
    {
        if (__state) return; // already activated before this call
        if (!CombatManager.Instance.IsInProgress) return;
        if (cardPlay.Card.Owner != __instance.Owner) return;
        if (cardPlay.Card.Type != CardType.Power) return;
        if (!(bool)ActivatedField.GetValue(__instance)!) return; // didn't trigger
        Track(__instance, s => s.Amount += __instance.DynamicVars.Block.IntValue);
    }
}
