using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Relics;
using RelicStats.Core;
using RelicStats.Relics;

namespace RelicStats.Patches;

internal static class CreatedCardProvenance
{
    internal static readonly CardAcquisitionLedger<CardModel, RelicModel> Ledger = new();

    internal static bool IsMeasured(RelicModel relic) =>
        relic is MoltenEgg or ToxicEgg or FrozenEgg or FresnelLens;

    internal static void RecordAcquisition(CardModel original, CardPileAddResult result)
    {
        bool acquired = result.success && result.cardAdded.Pile?.Type == PileType.Deck;
        foreach (var source in Ledger.Acquire(original, acquired))
        {
            // A direct-add upgrade has already been counted by that relic's existing hook.
            if (result.modifyingModels?.Contains(source) == true) continue;
            switch (source)
            {
                case MoltenEgg egg when result.cardAdded.CurrentUpgradeLevel > 0:
                    MoltenEggStats.Track(egg, stats => stats.Amount++);
                    break;
                case ToxicEgg egg when result.cardAdded.CurrentUpgradeLevel > 0:
                    ToxicEggStats.Track(egg, stats => stats.Amount++);
                    break;
                case FrozenEgg egg when result.cardAdded.CurrentUpgradeLevel > 0:
                    FrozenEggStats.Track(egg, stats => stats.Amount++);
                    break;
                case FresnelLens lens when result.cardAdded.Enchantment is Nimble:
                    FresnelLensStats.Track(lens, stats => stats.Amount++);
                    break;
            }
        }
    }
}

[HarmonyPatch]
internal static class CreatedCardModificationProvenancePatch
{
    public static IEnumerable<MethodBase> TargetMethods() =>
        typeof(CardCreationResult).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.Name == nameof(CardCreationResult.ModifyCard));

    public static void Postfix(CardCreationResult __instance) =>
        CreatedCardProvenance.Ledger.Record(__instance.Card,
            __instance.ModifyingRelics.Where(CreatedCardProvenance.IsMeasured));
}

[HarmonyPatch]
internal static class CreatedCardAcquisitionPatch
{
    public static IEnumerable<MethodBase> TargetMethods() =>
        typeof(CardPileCmd).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == nameof(CardPileCmd.Add)
                && method.GetParameters().Length >= 2
                && method.GetParameters()[0].ParameterType == typeof(IEnumerable<CardModel>)
                && method.GetParameters()[1].ParameterType == typeof(CardPile));

    public static void Prefix(IEnumerable<CardModel> cards, CardPile newPile, out CardModel[]? __state) =>
        __state = newPile.Type == PileType.Deck ? cards.ToArray() : null;

    public static void Postfix(ref Task<IReadOnlyList<CardPileAddResult>> __result, CardModel[]? __state)
    {
        if (__state != null) __result = Observe(__result, __state);
    }

    private static async Task<IReadOnlyList<CardPileAddResult>> Observe(
        Task<IReadOnlyList<CardPileAddResult>> task, CardModel[] originals)
    {
        var results = await task;
        for (int i = 0; i < originals.Length && i < results.Count; i++)
            CreatedCardProvenance.RecordAcquisition(originals[i], results[i]);
        return results;
    }
}
