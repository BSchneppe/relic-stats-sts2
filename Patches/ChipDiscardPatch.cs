using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using RelicStats.Core;

namespace RelicStats.Patches;

[HarmonyPatch(typeof(CardCmd), nameof(CardCmd.DiscardAndDraw))]
internal static class ChipDiscardCommandPatch
{
    public static void Prefix(IEnumerable<CardModel> cardsToDiscard, out IDisposable? __state)
    {
        var cards = cardsToDiscard.ToList();
        __state = cards.Count > 0 ? ChipDiscardScope.EnterCommand(cards[0].Owner, cards.Cast<object>()) : null;
    }
    public static void Finalizer(IDisposable? __state) => __state?.Dispose();
}

// DiscardAndDraw emits history even when combat ended and the move failed. Observe the
// successful move itself; selected-card identity prevents nested effects adding other cards.
[HarmonyPatch]
internal static class ChipDiscardFinishedPatch
{
    public static IEnumerable<MethodBase> TargetMethods() =>
        typeof(CardPileCmd).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == nameof(CardPileCmd.Add)
                && method.GetParameters().Length >= 2
                && method.GetParameters()[0].ParameterType == typeof(IEnumerable<CardModel>)
                && method.GetParameters()[1].ParameterType == typeof(CardPile));

    public static void Prefix(IEnumerable<CardModel> cards, CardPile newPile, out CardModel[]? __state) =>
        __state = newPile.Type == PileType.Discard ? cards.ToArray() : null;

    public static void Postfix(ref Task<IReadOnlyList<CardPileAddResult>> __result, CardModel[]? __state)
    {
        if (__state != null) __result = Observe(__result, __state);
    }

    private static async Task<IReadOnlyList<CardPileAddResult>> Observe(
        Task<IReadOnlyList<CardPileAddResult>> command, CardModel[] originals)
    {
        var results = await command;
        for (int i = 0; i < originals.Length && i < results.Count; i++)
            if (results[i].success) ChipDiscardScope.Discarded(originals[i]);
        return results;
    }
}
