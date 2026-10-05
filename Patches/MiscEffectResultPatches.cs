using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using RelicStats.Core;

namespace RelicStats.Patches;

[HarmonyPatch(typeof(PotionCmd), nameof(PotionCmd.TryToProcure),
    new[] { typeof(PotionModel), typeof(Player), typeof(int) })]
internal static class MiscPotionProcuredPatch
{
    public static void Prefix(Player player, out EffectScope<PotionProcureResult>.Lease? __state) =>
        __state = EffectScope<PotionProcureResult>.EnterCommand(player);

    public static void Postfix(ref Task<PotionProcureResult> __result, EffectScope<PotionProcureResult>.Lease? __state)
    {
        if (__state == null) return;
        __result = __state.Observe(__result);
        __state.Dispose();
    }

    public static void Finalizer(EffectScope<PotionProcureResult>.Lease? __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(CardCmd), nameof(CardCmd.Exhaust),
    new[] { typeof(PlayerChoiceContext), typeof(CardModel), typeof(bool), typeof(bool) })]
internal static class MiscCardExhaustedResultPatch
{
    public static void Prefix(CardModel card, out EffectScope<CardPileAddResult?>.Lease? __state) =>
        __state = EffectScope<CardPileAddResult?>.EnterCommand(card.Owner);

    public static void Postfix(ref Task<CardPileAddResult?> __result, EffectScope<CardPileAddResult?>.Lease? __state)
    {
        if (__state == null) return;
        __result = __state.Observe(__result);
        __state.Dispose();
    }

    public static void Finalizer(EffectScope<CardPileAddResult?>.Lease? __state) => __state?.Dispose();
}
