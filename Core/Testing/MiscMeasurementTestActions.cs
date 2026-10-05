#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace RelicStats.Core.Testing;

internal static class MiscMeasurementTestActions
{
    // Most counter fixtures deliberately omit inventory notifications. Dig uses real Obtain,
    // whose insertion index requires the inventory to reflect the player's live relic order.
    // Reconcile only this acquisition fixture through the game's normal removal/add events.
    internal static void PrepareShovelFixture()
    {
        var player = TestHelpers.Player ?? throw new InvalidOperationException("Shovel fixture has no player.");
        if (player.Relics.Any()) throw new InvalidOperationException("Shovel fixture requires the runner's empty relic set.");
        var inventory = NRun.Instance?.GlobalUi.RelicInventory
            ?? throw new InvalidOperationException("Shovel fixture requires the live relic inventory.");
        foreach (var holder in inventory.RelicNodes.ToArray())
        {
            var stale = holder.Relic.Model;
            player.AddRelicInternal(stale, silent: true);
            player.RemoveRelicInternal(stale, silent: false);
        }
        player.AddRelicInternal(ModelDb.Relic<Shovel>().ToMutable(), silent: false);
        TestHelpers.PushAutoCardSelector();
    }

    internal static void ClearShovelFixture()
    {
        TestHelpers.PopCardSelector();
        TestHelpers.CloseOverlays();
        var player = TestHelpers.Player;
        if (player == null) return;
        var inventory = NRun.Instance?.GlobalUi.RelicInventory;
        foreach (var relic in player.Relics.ToArray())
        {
            bool shown = inventory?.RelicNodes.Any(holder => ReferenceEquals(holder.Relic.Model, relic)) == true;
            player.RemoveRelicInternal(relic, silent: !shown);
        }
    }

    // Exercise the game's real generation hooks and the same Deck acquisition command used by
    // rewards and merchants. The unchosen option must never become a counted acquired card.
    internal static void RegisterCreatedCardAcquisitions(
        TestRunner runner, string relicId, string cardId, Func<int> amount, int baseline)
    {
        List<CardCreationResult>? options = null;
        Task<CardPileAddResult>? acquisition = null;
        foreach (bool merchant in new[] { false, true })
        {
            bool fromMerchant = merchant;
            int expectedBefore = baseline + (merchant ? 1 : 0);
            int expectedAfter = expectedBefore + 1;
            string label = merchant ? "merchant" : "reward";
            runner.Do($"generate two {label} options", () =>
            {
                var player = TestHelpers.Player!;
                var relic = player.Relics.Single(relic => relic.Id.Entry == relicId);
                var model = ModelDb.AllCards.Single(card => card.Id.Entry == cardId);
                options = Enumerable.Range(0, 2)
                    .Select(_ => new CardCreationResult(player.RunState.CreateCard(model, player))).ToList();
                if (fromMerchant) relic.ModifyMerchantCardCreationResults(player, options);
                else relic.TryModifyCardRewardOptionsLate(player, options,
                    CardCreationOptions.ForRoom(player, RoomType.Monster)
                        .WithFlags((Enum.TryParse<CardCreationFlags>("IsFromCombat", out var combatFlag)
                            ? combatFlag : default) | CardCreationFlags.IsCardReward));
            });
            runner.Assert($"{label} offers do not count as acquisitions", () =>
                new TestResult(amount() == expectedBefore,
                    $"expected {expectedBefore} before acquisition, got {amount()}"));
            runner.Do($"acquire one {label} option", () =>
                acquisition = CardPileCmd.Add(options![0].Card, PileType.Deck));
            runner.WaitUntil($"{label} acquisition completed", () =>
            {
                if (acquisition?.IsFaulted == true) throw acquisition.Exception!;
                return acquisition?.IsCompletedSuccessfully == true;
            }, 10000);
            runner.Assert($"only the acquired {label} option counted", () =>
                new TestResult(amount() == expectedAfter && options![0].Card.Pile?.Type == PileType.Deck
                    && options[1].Card.Pile?.Type != PileType.Deck,
                    $"expected {expectedAfter} with one acquired option, got {amount()}"));
        }
    }
}
#endif
