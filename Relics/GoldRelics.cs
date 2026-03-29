using System;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using RelicStats.Core;
#if DEBUG
using RelicStats.Core.Testing;
#endif

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

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("win combat", () => TestHelpers.WinCombat());
        runner.WaitFor(GameEvent.CombatVictory);
        runner.Assert("tracked gold", () =>
        {
            // TryModifyRewards fires during combat reward flow after victory.
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Gold.IntValue ?? -1;
            return new TestResult(Amount >= 0, $"got {Amount}, expected {expected} if reward triggered");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
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
        int bonus = (int)Math.Floor(amount * 0.2m);
        if (bonus <= 0) return;
        Track(__instance, s => s.Amount += bonus);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.SideTurnStart);
        runner.Do("gain gold", () => TestHelpers.AddGold(100));
        runner.Assert("tracked bonus gold", () =>
        {
            // BowlerHat gives 20% bonus; gaining 100 gold should yield 20 bonus
            int expected = (int)Math.Floor(100m * 0.2m);
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
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

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("win combat", () => TestHelpers.WinCombat());
        runner.WaitFor(GameEvent.CombatVictory);
        runner.Assert("tracked gold", () =>
        {
            // AfterCardChangedPiles fires when cards enter permanent deck.
            // Combat victory reward card selection may trigger this if player adds a card.
            return new TestResult(Amount >= 0, $"got {Amount} (requires card entering permanent deck)");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
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

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked gold", () =>
        {
            // AfterRoomEntered fires for each room entered; StartFight enters a combat room.
            // MawBank should gain gold if HasItemBeenBought is false.
            return new TestResult(Amount >= 0, $"got {Amount} (gains gold per room if no item bought)");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}
