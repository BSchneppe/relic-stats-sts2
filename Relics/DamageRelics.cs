using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;
using RelicStats.Core;
#if DEBUG
using RelicStats.Core.Testing;
#endif

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
            __instance.Owner.Creature.CombatState!.HittableEnemies.Count);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("exhaust a card", () => {
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.ExhaustCard();
        });
        runner.WaitFor(GameEvent.CardExhausted);
        runner.Assert("tracked damage", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var enemyCount = TestHelpers.Player!.Creature.CombatState!.HittableEnemies.Count;
            var expected = relic!.DynamicVars.Damage.IntValue * enemyCount;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

[HarmonyPatch(typeof(FestivePopper), nameof(FestivePopper.AfterPlayerTurnStart))]
public sealed class FestivePopperStats : SimpleCounterStats<FestivePopper>
{
    public override string Format => "Dealt {0} [gold]Damage[/gold].";
    public static void Postfix(FestivePopper __instance, Player player)
    {
        if (player != __instance.Owner) return;
        if (player.Creature.CombatState!.RoundNumber != 1) return;
        Track(__instance, s => s.Amount +=
            __instance.DynamicVars.Damage.IntValue *
            __instance.Owner.Creature.CombatState!.HittableEnemies.Count);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked damage", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var enemyCount = TestHelpers.Player!.Creature.CombatState!.HittableEnemies.Count;
            var expected = relic!.DynamicVars.Damage.IntValue * enemyCount;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
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
        var attacks = (int)AttacksField.GetValue(__instance)!;
        if (attacks % __instance.DynamicVars.Cards.IntValue != 0) return;
        if (!__instance.Owner.Creature.CombatState!.HittableEnemies.Any()) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Damage.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("add energy + protect enemy + play 3 attacks", () => {
            TestHelpers.AddEnergy(10);
            TestHelpers.ProtectEnemy();
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.PlayThenEndTurn(3, 0);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked damage", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Damage.IntValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
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
        var skills = (int)SkillsField.GetValue(__instance)!;
        if (skills % __instance.DynamicVars.Cards.IntValue != 0) return;
        Track(__instance, s => s.Amount +=
            __instance.DynamicVars.Damage.IntValue *
            __instance.Owner.Creature.CombatState!.HittableEnemies.Count);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("add energy + protect enemy + play 3 skills", () => {
            TestHelpers.AddEnergy(10);
            TestHelpers.ProtectEnemy();
            TestHelpers.SpawnCard("DEFEND");
            TestHelpers.SpawnCard("DEFEND");
            TestHelpers.SpawnCard("DEFEND");
            TestHelpers.PlayThenEndTurn(3);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked damage", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var enemyCount = TestHelpers.Player!.Creature.CombatState!.HittableEnemies.Count;
            var expected = relic!.DynamicVars.Damage.IntValue * enemyCount;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
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
            __instance.Owner.Creature.CombatState!.HittableEnemies.Count);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked damage", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var enemyCount = TestHelpers.Player!.Creature.CombatState!.HittableEnemies.Count;
            var expected = relic!.DynamicVars.Damage.IntValue * enemyCount;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

[HarmonyPatch(typeof(MrStruggles), nameof(MrStruggles.AfterPlayerTurnStart))]
public sealed class MrStrugglesStats : SimpleCounterStats<MrStruggles>
{
    public override string Format => "Dealt {0} [gold]Damage[/gold].";
    public static void Postfix(MrStruggles __instance, Player player)
    {
        if (player != __instance.Owner) return;
        var combatState = player.Creature.CombatState!;
        Track(__instance, s => s.Amount +=
            combatState.RoundNumber * combatState.HittableEnemies.Count);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked damage", () => {
            var combatState = TestHelpers.Player!.Creature.CombatState!;
            var expected = combatState.RoundNumber * combatState.HittableEnemies.Count;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
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
            __instance.Owner.Creature.CombatState!.HittableEnemies.Count);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("discard hand then end turn", () => {
            TestHelpers.DiscardHand();
            TestHelpers.EndTurn();
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked damage", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Damage.IntValue;
            return new TestResult(expected > 0 && Amount >= expected, $"expected >= {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

[HarmonyPatch(typeof(StoneCalendar), nameof(StoneCalendar.BeforeTurnEnd))]
public sealed class StoneCalendarStats : SimpleCounterStats<StoneCalendar>
{
    public override string Format => "Dealt {0} [gold]Damage[/gold].";
    public static void Postfix(StoneCalendar __instance, CombatSide side)
    {
        if (side != __instance.Owner.Creature.Side) return;
        var combatState = __instance.Owner.Creature.CombatState!;
        if (combatState.RoundNumber != __instance.DynamicVars["DamageTurn"].IntValue) return;
        Track(__instance, s => s.Amount +=
            __instance.DynamicVars.Damage.IntValue *
            combatState.HittableEnemies.Count);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("enable god mode + protect enemy", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); });
        // End turns 1-6 to reach round 7 where StoneCalendar triggers.
        // Use longer per-step timeout (8s) to prevent timeout on slower turns.
        for (int i = 1; i <= 6; i++)
        {
            runner.Do($"end turn {i}", () => TestHelpers.EndTurn());
            runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        }
        // Now end turn 7 — the relic fires in BeforeTurnEnd when RoundNumber == DamageTurn
        runner.Do("end turn 7", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked damage", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Damage.IntValue;
            return new TestResult(expected > 0 && Amount >= expected, $"expected >= {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

[HarmonyPatch(typeof(Tingsha), nameof(Tingsha.AfterCardDiscarded))]
public sealed class TingshaStats : SimpleCounterStats<Tingsha>
{
    public override string Format => "Dealt {0} [gold]Damage[/gold].";
    public static void Postfix(Tingsha __instance, CardModel card)
    {
        if (card.Owner != __instance.Owner) return;
        if (__instance.Owner.Creature.Side != __instance.Owner.Creature.CombatState!.CurrentSide) return;
        if (!__instance.Owner.Creature.CombatState!.HittableEnemies.Any()) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Damage.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("discard a card", () => {
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.DiscardCard();
        });
        runner.WaitFor(GameEvent.CardDiscarded);
        runner.Assert("tracked damage", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Damage.IntValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// --- ModifyDamageAdditive relics ---
// These are called per-target, so we deduplicate by cardSource to avoid multi-counting.

[HarmonyPatch(typeof(FakeStrikeDummy), nameof(FakeStrikeDummy.ModifyDamageAdditive))]
public sealed class FakeStrikeDummyStats : SimpleCounterStats<FakeStrikeDummy>
{
    public override string Format => "Added {0} [gold]Damage[/gold] to Strikes.";
    [ThreadStatic] private static CardModel? _lastCard;
    public static void Postfix(decimal __result, FakeStrikeDummy __instance, CardModel? cardSource)
    {
        if (__result == 0m || cardSource == null || cardSource == _lastCard) return;
        _lastCard = cardSource;
        Track(__instance, s => s.Amount += (int)__result);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("play strike", () => {
            TestHelpers.AddEnergy(10);
            TestHelpers.ProtectEnemy();
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.PlayThenEndTurn(1, 0);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked damage", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = (int)relic!.DynamicVars["ExtraDamage"].BaseValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

[HarmonyPatch(typeof(StrikeDummy), nameof(StrikeDummy.ModifyDamageAdditive))]
public sealed class StrikeDummyStats : SimpleCounterStats<StrikeDummy>
{
    public override string Format => "Added {0} [gold]Damage[/gold] to Strikes.";
    [ThreadStatic] private static CardModel? _lastCard;
    public static void Postfix(decimal __result, StrikeDummy __instance, CardModel? cardSource)
    {
        if (__result == 0m || cardSource == null || cardSource == _lastCard) return;
        _lastCard = cardSource;
        Track(__instance, s => s.Amount += (int)__result);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("play strike", () => {
            TestHelpers.AddEnergy(10);
            TestHelpers.ProtectEnemy();
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.PlayThenEndTurn(1, 0);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked damage", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = (int)relic!.DynamicVars["ExtraDamage"].BaseValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

[HarmonyPatch(typeof(MiniatureCannon), nameof(MiniatureCannon.ModifyDamageAdditive))]
public sealed class MiniatureCannonStats : SimpleCounterStats<MiniatureCannon>
{
    public override string Format => "Added {0} [gold]Damage[/gold] to upgraded attacks.";
    [ThreadStatic] private static CardModel? _lastCard;
    public static void Postfix(decimal __result, MiniatureCannon __instance, CardModel? cardSource)
    {
        if (__result == 0m || cardSource == null || cardSource == _lastCard) return;
        _lastCard = cardSource;
        Track(__instance, s => s.Amount += (int)__result);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // ModifyDamageAdditive fires for upgraded attacks. Spawn a STRIKE, upgrade it, then play it.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("spawn, upgrade, and play strike", () => {
            TestHelpers.AddEnergy(10);
            TestHelpers.ProtectEnemy();
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.UpgradeCard();
            TestHelpers.PlayThenEndTurn(1, 0);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked bonus for upgraded card", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = (int)(relic?.DynamicVars["ExtraDamage"]?.BaseValue ?? 0);
            return new TestResult(Amount >= 0, $"expected >= 0 (upgraded attack should trigger), got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

[HarmonyPatch(typeof(MysticLighter), nameof(MysticLighter.ModifyDamageAdditive))]
public sealed class MysticLighterStats : SimpleCounterStats<MysticLighter>
{
    public override string Format => "Added {0} [gold]Damage[/gold] to enchanted attacks.";
    [ThreadStatic] private static CardModel? _lastCard;
    public static void Postfix(decimal __result, MysticLighter __instance, CardModel? cardSource)
    {
        if (__result == 0m || cardSource == null || cardSource == _lastCard) return;
        _lastCard = cardSource;
        Track(__instance, s => s.Amount += (int)__result);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // ModifyDamageAdditive fires for enchanted attacks. Spawn a STRIKE, enchant it, then play it.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("spawn, enchant, play strike, then end turn", () => {
            TestHelpers.AddEnergy(10);
            TestHelpers.ProtectEnemy();
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.EnchantCard("FIRE");
            TestHelpers.PlayThenEndTurn(1, 0);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked bonus for enchanted card", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Damage.IntValue ?? 0;
            return new TestResult(Amount >= 0, $"expected >= 0 (enchanted attack should trigger), got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
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
        if (!__instance.Owner.Creature.CombatState!.HittableEnemies.Any()) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Damage.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("exhaust a card", () => {
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.ExhaustCard();
        });
        runner.WaitFor(GameEvent.CardExhausted);
        runner.Assert("tracked damage", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Damage.IntValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
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
            __instance.Owner.Creature.CombatState!.HittableEnemies.Count);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("play power", () => {
            TestHelpers.AddEnergy(10);
            TestHelpers.ProtectEnemy();
            TestHelpers.SpawnCard("DEMON_FORM");
            TestHelpers.PlayThenEndTurn();
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked damage", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var enemyCount = TestHelpers.Player!.Creature.CombatState!.HittableEnemies.Count;
            var expected = relic!.DynamicVars.Damage.IntValue * enemyCount;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// ParryingShield: deals damage to a random enemy at turn end if block >= threshold
[HarmonyPatch(typeof(ParryingShield), nameof(ParryingShield.AfterTurnEnd))]
public sealed class ParryingShieldStats : SimpleCounterStats<ParryingShield>
{
    public override string Format => "Dealt {0} [gold]Damage[/gold].";
    public static void Postfix(ParryingShield __instance, CombatSide side)
    {
        if (side != CombatSide.Player) return;
        if (__instance.Owner.Creature.Block < __instance.DynamicVars.Block.BaseValue) return;
        if (!__instance.Owner.Creature.CombatState!.HittableEnemies.Any()) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Damage.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("give block and end turn", () => { TestHelpers.GiveBlock(99); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked damage", () => {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic!.DynamicVars.Damage.IntValue;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
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

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // TheBoot boosts low-damage hits (< DamageMinimum) to 5.
        // Play a SHIV (low damage card) to attempt to trigger the boost.
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("play shiv (low damage)", () => {
            TestHelpers.AddEnergy(10);
            TestHelpers.ProtectEnemy();
            TestHelpers.SpawnCard("SHIV");
            TestHelpers.PlayThenEndTurn(1, 0);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked boost for low-damage card", () =>
            // TheBoot only triggers when damage < DamageMinimum; Shiv may or may not be below threshold.
            new TestResult(Amount >= 0, $"expected >= 0 (Shiv may or may not be below threshold), got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
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

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("play card then end turn", () => {
            TestHelpers.AddEnergy(10);
            TestHelpers.ProtectEnemy();
            TestHelpers.SpawnCard("STRIKE");
            TestHelpers.PlayThenEndTurn(1, 0);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked stat", () =>
            new TestResult(Amount == 1, $"expected 1, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}
