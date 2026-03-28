using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using RelicStats.Core;

namespace RelicStats.Relics;

// AmethystAubergine: extra gold reward from non-boss combat rooms
[HarmonyPatch(typeof(AmethystAubergine), nameof(AmethystAubergine.TryModifyRewards))]
public sealed class AmethystAubergineStats : SimpleCounterStats<AmethystAubergine>
{
    public override string Format => "Gained {0} [gold]Gold[/gold].";
    public static void Postfix(AmethystAubergine __instance, bool __result)
    {
        if (!__result) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Gold.IntValue);
    }
}

// BowlerHat: 20% bonus gold on all gold gains
// We track in ShouldGainGold where we can see the original amount before bonus is applied
[HarmonyPatch(typeof(BowlerHat), nameof(BowlerHat.ShouldGainGold))]
public sealed class BowlerHatStats : SimpleCounterStats<BowlerHat>
{
    public override string Format => "Gained {0} bonus [gold]Gold[/gold].";
    public static void Postfix(BowlerHat __instance, decimal amount, Player player)
    {
        if (player != __instance.Owner) return;
        // BowlerHat internally tracks _isApplyingBonus to avoid recursion;
        // when _isApplyingBonus is true, this is the bonus gold being applied, not original.
        // We detect the original call (not the recursive bonus call) by checking the field.
        var isApplyingField = AccessTools.Field(typeof(BowlerHat), "_isApplyingBonus");
        if ((bool)isApplyingField.GetValue(__instance)!) return;
        int bonus = (int)System.Math.Floor(amount * 0.2m);
        if (bonus <= 0) return;
        Track(__instance, s => s.Amount += bonus);
    }
}

// LuckyFysh: gains gold when cards enter deck
[HarmonyPatch(typeof(LuckyFysh), nameof(LuckyFysh.AfterCardChangedPiles))]
public sealed class LuckyFyshStats : SimpleCounterStats<LuckyFysh>
{
    public override string Format => "Gained {0} [gold]Gold[/gold].";
    public static void Postfix(LuckyFysh __instance, CardModel card)
    {
        CardPile? pile = card.Pile;
        if (pile == null || pile.Type != PileType.Deck) return;
        if (card.Owner != __instance.Owner) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Gold.IntValue);
    }
}

// MawBank: gains gold when entering each room (until an item is bought)
[HarmonyPatch(typeof(MawBank), nameof(MawBank.AfterRoomEntered))]
public sealed class MawBankStats : SimpleCounterStats<MawBank>
{
    public override string Format => "Gained {0} [gold]Gold[/gold].";
    public static void Postfix(MawBank __instance, AbstractRoom room)
    {
        if (__instance.HasItemBeenBought) return;
        if (__instance.Owner.RunState.BaseRoom != room) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Gold.IntValue);
    }
}
