using System.Collections.Generic;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using RelicStats.Core;

namespace RelicStats.Patches;

[HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.Draw),
    new[] { typeof(PlayerChoiceContext), typeof(decimal), typeof(Player), typeof(bool) })]
internal static class DirectRelicDrawPatch
{
    public static void Prefix(Player player, out EffectScope<int>.Lease? __state) =>
        __state = DirectDrawScope.EnterCommand(player);

    public static void Postfix(ref Task<IEnumerable<CardModel>> __result, EffectScope<int>.Lease? __state)
    {
        if (__state != null) __result = DirectDrawScope.CountCommand(__result, __state);
    }

    public static void Finalizer(EffectScope<int>.Lease? __state) => __state?.Dispose();
}
