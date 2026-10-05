using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using RelicStats.Core;

namespace RelicStats.Patches;

[HarmonyPatch(typeof(CardCmd), nameof(CardCmd.AutoPlay))]
internal static class RelicAutoPlayPatch
{
    public static void Prefix(CardModel card, out IDisposable? __state) =>
        __state = AutoPlayScope.EnterCommand(card.Owner, card);

    public static void Finalizer(IDisposable? __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.CardPlayFinished))]
internal static class RelicAutoPlayFinishedPatch
{
    public static void Postfix(CardPlay cardPlay) =>
        AutoPlayScope.Finished(cardPlay.Card, cardPlay.IsAutoPlay && cardPlay.PlayIndex == 0);
}
