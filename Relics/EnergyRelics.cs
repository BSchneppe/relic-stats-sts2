using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using RelicStats.Core;
using RelicStats.Patches;
#if DEBUG
using RelicStats.Core.Testing;
#endif

namespace RelicStats.Relics;

// ── Simple energy relics ──────────────────────────────────────────────

// ArtOfWar: gains energy if no attacks played last turn
[HarmonyPatch(typeof(ArtOfWar), nameof(ArtOfWar.AfterEnergyReset))]
public sealed class ArtOfWarStats : SimpleCounterStats<ArtOfWar>
{
    private static readonly FieldInfo AttacksField =
        AccessTools.Field(typeof(ArtOfWar), "_anyAttacksPlayedLastTurn");

    [ThreadStatic] private static bool _hadNoAttacks;

    public override string Format => "Generated {0} [gold]Energy[/gold].";

    public static void Prefix(ArtOfWar __instance, Player player)
    {
        _hadNoAttacks = false;
        if (player != __instance.Owner) return;
        if (__instance.Owner.PlayerCombatState!.TurnNumber <= 1) return;
        _hadNoAttacks = !(bool)AttacksField.GetValue(__instance)!;
    }

    public static void Postfix(ArtOfWar __instance, Player player)
    {
        if (!_hadNoAttacks) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Energy.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("enable god mode + protect enemy", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); });
        int Expected() => TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId)?.DynamicVars.Energy.IntValue ?? -1;
        // No attacks on turn 1, so the turn-2 AfterEnergyReset sees _anyAttacksPlayedLastTurn == false.
        runner.Do("end turn 1 without attacking", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked energy on turn 2", () =>
        {
            var expected = Expected();
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        // An attack on turn 2 flips the flag: the turn-3 reset must not count.
        runner.Do("play an attack, end turn 2", () =>
        {
            TestHelpers.SpawnCard("STRIKE_IRONCLAD");
            TestHelpers.PlayThenEndTurn(1, 0);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("no energy after an attack turn", () =>
        {
            var expected = Expected();
            return new TestResult(Amount == expected, $"expected still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// HappyFlower: gains energy every 3rd turn
[HarmonyPatch(typeof(HappyFlower), nameof(HappyFlower.AfterSideTurnStart))]
public sealed class HappyFlowerStats : SimpleCounterStats<HappyFlower>
{
    private static readonly FieldInfo TurnsSeenField =
        AccessTools.Field(typeof(HappyFlower), "_turnsSeen");

    public override string Format => "Generated {0} [gold]Energy[/gold].";
    public static void Postfix(HappyFlower __instance, CombatSide side)
    {
#if DEBUG
        if (TestManager.IsRunning)
            MainFile.Logger.Info($"[HappyFlower Postfix] side={side} ownerSide={__instance.Owner?.Creature?.Side} turnsSeen={(int)TurnsSeenField.GetValue(__instance)!}");
#endif
        if (side != __instance.Owner!.Creature.Side) return;
        var turnsSeen = (int)TurnsSeenField.GetValue(__instance)!;
        if (turnsSeen != 0) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Energy.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("enable god mode + protect enemy", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); });
        // The relic counts every player side-turn start (turns 1, 2, 3 -> TurnsSeen 1, 2, 0) and fires when
        // the counter wraps, i.e. on turn 3. After K EndTurns + PlayerTurnStart, AfterSideTurnStart has run
        // for turns 1..K only: two EndTurns show nothing yet, the third shows the turn-3 trigger.
        for (int i = 1; i <= 2; i++)
        {
            runner.Do($"end turn {i}", () => TestHelpers.EndTurn());
            runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        }
        runner.Assert("nothing before turn 3", () => new TestResult(Amount == 0, $"expected 0, got {Amount}"));
        runner.Do("end turn 3", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked energy on turn 3", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Energy.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// FakeHappyFlower: gains energy every 5th turn
[HarmonyPatch(typeof(FakeHappyFlower), nameof(FakeHappyFlower.AfterSideTurnStart))]
public sealed class FakeHappyFlowerStats : SimpleCounterStats<FakeHappyFlower>
{
    private static readonly FieldInfo TurnsSeenField =
        AccessTools.Field(typeof(FakeHappyFlower), "_turnsSeen");

    public override string Format => "Generated {0} [gold]Energy[/gold].";
    public static void Postfix(FakeHappyFlower __instance, CombatSide side)
    {
        if (side != __instance.Owner.Creature.Side) return;
        var turnsSeen = (int)TurnsSeenField.GetValue(__instance)!;
        if (turnsSeen != 0) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Energy.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("enable god mode + protect enemy", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); });
        // Same counter as HappyFlower with a 5-turn wrap: the turn-5 trigger is visible only after the
        // fifth EndTurn (AfterSideTurnStart has run for turns 1..K after K EndTurns).
        for (int i = 1; i <= 4; i++)
        {
            runner.Do($"end turn {i}", () => TestHelpers.EndTurn());
            runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        }
        runner.Assert("nothing before turn 5", () => new TestResult(Amount == 0, $"expected 0, got {Amount}"));
        runner.Do("end turn 5", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked energy on turn 5", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Energy.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// PaelsTears: gains energy if unspent energy last turn
[HarmonyPatch(typeof(PaelsTears), nameof(PaelsTears.AfterSideTurnStart))]
public sealed class PaelsTearStats : SimpleCounterStats<PaelsTears>
{
    private static readonly FieldInfo HadLeftoverField =
        AccessTools.Field(typeof(PaelsTears), "_hadLeftoverEnergy");

    public override string Format => "Generated {0} [gold]Energy[/gold].";
    public static void Postfix(PaelsTears __instance, CombatSide side)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (!(bool)HadLeftoverField.GetValue(__instance)!) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Energy.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("enable god mode + protect enemy", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); });
        int Expected() => TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId)?.DynamicVars.Energy.IntValue ?? -1;
        // BeforeSideTurnEnd of turn 1 records the unspent energy; the turn-2 AfterSideTurnStart pays it out.
        // After K EndTurns + PlayerTurnStart, AfterSideTurnStart has run for turns 1..K only.
        runner.Do("end turn 1 with energy left", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("nothing before the turn-2 side start", () => new TestResult(Amount == 0, $"expected 0, got {Amount}"));
        runner.Do("end turn 2 with energy left", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked energy on turn 2", () =>
        {
            var expected = Expected();
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        // Turn 2 also ended with energy left, so turn 3's side start pays out again. Wait for it, then
        // spend every point on turn 3: turn 4 must not pay.
        runner.WaitFor(GameEvent.SideTurnStart, 15000);
        runner.Do("spend all energy on turn 3, end turn", () =>
        {
            int energy = TestHelpers.Player!.PlayerCombatState!.Energy;
            if (energy <= 0) { TestHelpers.EndTurn(); return; }
            for (int i = 0; i < energy; i++) TestHelpers.SpawnCard("STRIKE_IRONCLAD");
            TestHelpers.PlayThenEndTurn(energy, 0);
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.WaitFor(GameEvent.SideTurnStart, 15000);
        runner.Assert("no energy after a turn with none left over", () =>
        {
            var expected = 2 * Expected();
            return new TestResult(expected > 0 && Amount == expected, $"expected still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// PrismaticGem: +1 max energy
[HarmonyPatch(typeof(PrismaticGem), nameof(PrismaticGem.ModifyMaxEnergy))]
public sealed class PrismaticGemStats : SimpleCounterStats<PrismaticGem>
{
    public override string Format => "Generated {0} [gold]Energy[/gold].";
    public static void Postfix(PrismaticGem __instance, decimal __result, decimal __1)
    {
        int delta = (int)(__result - __1);
        // MaxEnergy is computed, so this also fires on every UI read; count only real grants.
        if (delta <= 0 || !EnergyGrantScope.IsCounting) return;
        Track(__instance, s => s.Amount += delta);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        // Energy is credited at the turn-start refill, which is after CombatStart.
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked energy", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Energy.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// PumpkinCandle: +1 max energy while kindled (5 combats after pickup or a rest-site Kindle)
[HarmonyPatch(typeof(PumpkinCandle), nameof(PumpkinCandle.ModifyMaxEnergy))]
public sealed class PumpkinCandleStats : SimpleCounterStats<PumpkinCandle>
{
    public override string Format => "Generated {0} [gold]Energy[/gold].";
    public static void Postfix(PumpkinCandle __instance, decimal __result, decimal __1)
    {
        int delta = (int)(__result - __1);
        // MaxEnergy is computed, so this also fires on every UI read; count only real grants.
        if (delta <= 0 || !EnergyGrantScope.IsCounting) return;
        Track(__instance, s => s.Amount += delta);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        // AddRelic never fires AfterObtained, so KindleCount stays 0 and ModifyMaxEnergy is a no-op:
        // the unkindled relic must not count. Rekindle() is what AfterObtained calls on pickup.
        runner.Do("add relic (unkindled)", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("unkindled candle gives nothing", () => new TestResult(Amount == 0, $"expected 0, got {Amount}"));
        runner.Do("kindle + end turn", () =>
        {
            TestHelpers.GetRelic<PumpkinCandle>()!.Rekindle();
            TestHelpers.EnableGodMode();
            TestHelpers.ProtectEnemy();
            TestHelpers.EndTurn();
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked energy once kindled", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Energy.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// Lantern: gains energy turn 1
[HarmonyPatch(typeof(Lantern), nameof(Lantern.AfterSideTurnStart))]
public sealed class LanternStats : SimpleCounterStats<Lantern>
{
    public override string Format => "Generated {0} [gold]Energy[/gold].";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(Lantern __instance, CombatSide side, ICombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (__instance.Owner.PlayerCombatState!.TurnNumber > 1) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Energy.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.SideTurnStart);
        int Expected() => TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId)?.DynamicVars.Energy.IntValue ?? -1;
        runner.Assert("tracked energy on turn 1", () =>
        {
            var expected = Expected();
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        // Turn 1 only: the turn-2 side start (after the next PlayerTurnStart) must not count again.
        runner.Do("god mode + protect enemy + end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.WaitFor(GameEvent.SideTurnStart, 15000);
        runner.Assert("still turn-1 energy only", () =>
        {
            var expected = Expected();
            return new TestResult(Amount == expected, $"expected still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// IceCream: preserves energy (tracks when energy reset is prevented)
[HarmonyPatch(typeof(IceCream), nameof(IceCream.ShouldPlayerResetEnergy))]
public sealed class IceCreamStats : SimpleCounterStats<IceCream>
{
    public override string Format => "Preserved energy {0} times.";
    public static void Postfix(IceCream __instance, Player player, bool __result)
    {
        if (player != __instance.Owner) return;
        if (__result) return; // energy was reset, relic did not trigger
        Track(__instance, s => s.Amount++);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        // ShouldPlayerResetEnergy returns true on turn 1 (energy IS reset), so nothing is counted yet.
        runner.Assert("turn 1 resets energy normally", () => new TestResult(Amount == 0, $"expected 0, got {Amount}"));
        runner.Do("enable god mode + protect enemy", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); });
        // Turns 2 and 3 keep their energy: one count per turn setup, already run at each PlayerTurnStart.
        runner.Do("end turn 1", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Do("end turn 2", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("preserved energy on turns 2 and 3", () => new TestResult(Amount == 2, $"expected 2, got {Amount}"));
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// ── Complex energy relics ─────────────────────────────────────────────

// PhilosophersStone: +1 energy, gives enemies strength
[HarmonyPatch]
public sealed class PhilosophersStoneStats : IRelicStats
{
    public string RelicId => RelicIdHelper.Slugify(nameof(PhilosophersStone));

    public int EnergyGenerated { get; set; }
    public int StrengthGiven { get; set; }

    public string GetDescription(int effectiveTurns, int effectiveCombats)
    {
        return $"Generated {Fmt.Blue(EnergyGenerated)} [gold]Energy[/gold]. " +
               $"Gave enemies {Fmt.Blue(StrengthGiven)} {Fmt.Strength}.";
    }

    public JsonObject Save()
    {
        var obj = new JsonObject
        {
            ["energy"] = EnergyGenerated,
            ["strength"] = StrengthGiven,
        };
        return obj;
    }

    public void Load(JsonObject data)
    {
        EnergyGenerated = data["energy"]?.GetValue<int>() ?? 0;
        StrengthGiven = data["strength"]?.GetValue<int>() ?? 0;
    }

    public void Reset()
    {
        EnergyGenerated = 0;
        StrengthGiven = 0;
    }

    private static bool TryGet(PhilosophersStone instance, out PhilosophersStoneStats stats)
    {
        stats = null!;
        if (instance.IsMelted) return false;
        if (!LocalContext.IsMine(instance)) return false;
        if (RelicStatsRegistry.Get(RelicIdHelper.Slugify(nameof(PhilosophersStone))) is not PhilosophersStoneStats s) return false;
        stats = s;
        return true;
    }

    [HarmonyPatch(typeof(PhilosophersStone), nameof(PhilosophersStone.ModifyMaxEnergy))]
    [HarmonyPostfix]
    public static void ModifyMaxEnergyPostfix(PhilosophersStone __instance, decimal __result, decimal __1)
    {
        int delta = (int)(__result - __1);
        // MaxEnergy is computed, so this also fires on every UI read; count only real grants.
        if (delta <= 0 || !EnergyGrantScope.IsCounting) return;
        if (!TryGet(__instance, out var stats)) return;
        stats.EnergyGenerated += delta;
    }

    [HarmonyPatch(typeof(PhilosophersStone), nameof(PhilosophersStone.AfterCreatureAddedToCombat))]
    [HarmonyPostfix]
    public static void AfterCreatureAddedPostfix(PhilosophersStone __instance, Creature creature)
    {
        if (creature.Side == __instance.Owner.Creature.Side) return;
        if (!TryGet(__instance, out var stats)) return;
        stats.StrengthGiven += (int)__instance.DynamicVars["StrengthPower"].BaseValue;
    }

    [HarmonyPatch(typeof(PhilosophersStone), nameof(PhilosophersStone.AfterRoomEntered))]
    [HarmonyPostfix]
    public static void AfterRoomEnteredPostfix(PhilosophersStone __instance, AbstractRoom room)
    {
        if (room is not CombatRoom) return;
        if (!TryGet(__instance, out var stats)) return;
        int enemies = __instance.Owner!.Creature.CombatState!
            .GetOpponentsOf(__instance.Owner.Creature)
            .Count(c => c.IsAlive);
        stats.StrengthGiven += (int)__instance.DynamicVars["StrengthPower"].BaseValue * enemies;
    }

#if DEBUG
    public void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        // Energy is credited at the turn-start refill, which is after CombatStart. Strength is given to
        // every enemy present at AfterRoomEntered (one Nibbit here); AfterCreatureAddedToCombat only
        // fires for mid-combat spawns (CreatureCmd.Add), not the initial encounter.
        runner.WaitFor(GameEvent.PlayerTurnStart);
        int Energy() => TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId)?.DynamicVars.Energy.IntValue ?? -1;
        int Strength() => (int)(TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId)?.DynamicVars["StrengthPower"].BaseValue ?? -1);
        int Enemies() => TestHelpers.Player!.Creature.CombatState!.HittableEnemies.Count;
        runner.Assert("tracked energy and strength on turn 1", () =>
        {
            var energy = Energy(); var strength = Strength() * Enemies();
            return new TestResult(energy > 0 && strength > 0 && EnergyGenerated == energy && StrengthGiven == strength,
                $"expected energy {energy} / strength {strength}, got EnergyGenerated={EnergyGenerated}, StrengthGiven={StrengthGiven}");
        });
        // Energy repeats every turn; strength was a one-off at room entry.
        runner.Do("god mode + protect enemy + end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("energy again on turn 2, strength unchanged", () =>
        {
            var energy = 2 * Energy(); var strength = Strength() * Enemies();
            return new TestResult(EnergyGenerated == energy && StrengthGiven == strength,
                $"expected energy {energy} / strength still {strength}, got EnergyGenerated={EnergyGenerated}, StrengthGiven={StrengthGiven}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// BlessedAntler: +1 energy, adds Dazed cards turn 1
[HarmonyPatch]
public sealed class BlessedAntlerStats : IRelicStats
{
    public string RelicId => RelicIdHelper.Slugify(nameof(BlessedAntler));

    public int EnergyGenerated { get; set; }
    public int DazedAdded { get; set; }

    public string GetDescription(int effectiveTurns, int effectiveCombats)
    {
        return $"Generated {Fmt.Blue(EnergyGenerated)} [gold]Energy[/gold]. " +
               $"Added {Fmt.Blue(DazedAdded)} Dazed cards.";
    }

    public JsonObject Save()
    {
        var obj = new JsonObject
        {
            ["energy"] = EnergyGenerated,
            ["dazed"] = DazedAdded,
        };
        return obj;
    }

    public void Load(JsonObject data)
    {
        EnergyGenerated = data["energy"]?.GetValue<int>() ?? 0;
        DazedAdded = data["dazed"]?.GetValue<int>() ?? 0;
    }

    public void Reset()
    {
        EnergyGenerated = 0;
        DazedAdded = 0;
    }

    private static bool TryGet(BlessedAntler instance, out BlessedAntlerStats stats)
    {
        stats = null!;
        if (instance.IsMelted) return false;
        if (!LocalContext.IsMine(instance)) return false;
        if (RelicStatsRegistry.Get(RelicIdHelper.Slugify(nameof(BlessedAntler))) is not BlessedAntlerStats s) return false;
        stats = s;
        return true;
    }

    [HarmonyPatch(typeof(BlessedAntler), nameof(BlessedAntler.ModifyMaxEnergy))]
    [HarmonyPostfix]
    public static void ModifyMaxEnergyPostfix(BlessedAntler __instance, decimal __result, decimal __1)
    {
        int delta = (int)(__result - __1);
        // MaxEnergy is computed, so this also fires on every UI read; count only real grants.
        if (delta <= 0 || !EnergyGrantScope.IsCounting) return;
        if (!TryGet(__instance, out var stats)) return;
        stats.EnergyGenerated += delta;
    }

    [HarmonyPatch(typeof(BlessedAntler), nameof(BlessedAntler.BeforeHandDraw))]
    [HarmonyPostfix]
    public static void BeforeHandDrawPostfix(BlessedAntler __instance, Player player, ICombatState combatState)
    {
        if (player != __instance.Owner) return;
        if (__instance.Owner.PlayerCombatState!.TurnNumber != 1) return;
        if (!TryGet(__instance, out var stats)) return;
        stats.DazedAdded += __instance.DynamicVars.Cards.IntValue;
    }

#if DEBUG
    public void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        // BeforeHandDraw (turn 1 only) adds the Dazed and waits 3s before the hand draw, hence 15s.
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        int Energy() => TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId)?.DynamicVars.Energy.IntValue ?? -1;
        int Cards() => TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId)?.DynamicVars.Cards.IntValue ?? -1;
        runner.Assert("tracked energy and dazed on turn 1", () =>
        {
            var energy = Energy(); var cards = Cards();
            return new TestResult(energy > 0 && cards > 0 && EnergyGenerated == energy && DazedAdded == cards,
                $"expected energy {energy} / dazed {cards}, got EnergyGenerated={EnergyGenerated}, DazedAdded={DazedAdded}");
        });
        // Turn 2's BeforeHandDraw has already run at the next PlayerTurnStart: no more Dazed, energy again.
        runner.Do("god mode + protect enemy + end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("energy again on turn 2, dazed unchanged", () =>
        {
            var energy = 2 * Energy(); var cards = Cards();
            return new TestResult(EnergyGenerated == energy && DazedAdded == cards,
                $"expected energy {energy} / dazed still {cards}, got EnergyGenerated={EnergyGenerated}, DazedAdded={DazedAdded}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// BloodSoakedRose: +1 energy, adds an Enthralled curse to the deck on pickup. The curse is always
// exactly one, so it is not a stat worth showing; only the energy is tracked.
[HarmonyPatch]
public sealed class BloodSoakedRoseStats : IRelicStats
{
    public string RelicId => RelicIdHelper.Slugify(nameof(BloodSoakedRose));

    public int EnergyGenerated { get; set; }

    public string GetDescription(int effectiveTurns, int effectiveCombats) =>
        $"Generated {Fmt.Blue(EnergyGenerated)} [gold]Energy[/gold].";

    // Older saves also carry an "enthrall" count; Load ignores it.
    public JsonObject Save() => new() { ["energy"] = EnergyGenerated };

    public void Load(JsonObject data)
    {
        EnergyGenerated = data["energy"]?.GetValue<int>() ?? 0;
    }

    public void Reset()
    {
        EnergyGenerated = 0;
    }

    private static bool TryGet(BloodSoakedRose instance, out BloodSoakedRoseStats stats)
    {
        stats = null!;
        if (instance.IsMelted) return false;
        if (!LocalContext.IsMine(instance)) return false;
        if (RelicStatsRegistry.Get(RelicIdHelper.Slugify(nameof(BloodSoakedRose))) is not BloodSoakedRoseStats s) return false;
        stats = s;
        return true;
    }

    [HarmonyPatch(typeof(BloodSoakedRose), nameof(BloodSoakedRose.ModifyMaxEnergy))]
    [HarmonyPostfix]
    public static void ModifyMaxEnergyPostfix(BloodSoakedRose __instance, decimal __result, decimal __1)
    {
        int delta = (int)(__result - __1);
        // MaxEnergy is computed, so this also fires on every UI read; count only real grants.
        if (delta <= 0 || !EnergyGrantScope.IsCounting) return;
        if (!TryGet(__instance, out var stats)) return;
        stats.EnergyGenerated += delta;
    }

#if DEBUG
    public void RegisterTest(TestRunner runner)
    {
        int expected = -1;
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        // Energy is credited at the turn-start refill, which is after CombatStart.
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked energy on turn 1", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            expected = relic?.DynamicVars.Energy.IntValue ?? -1;
            return new TestResult(expected > 0 && EnergyGenerated == expected,
                $"expected {expected}, got {EnergyGenerated}");
        });
        // One grant per turn-start refill, and nothing else: the turn-2 refill adds exactly one more.
        runner.Do("end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("one more grant on turn 2", () =>
            new TestResult(EnergyGenerated == 2 * expected, $"expected {2 * expected}, got {EnergyGenerated}"));
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// Bread: loses energy turn 1, gains energy other turns
[HarmonyPatch]
public sealed class BreadStats : IRelicStats
{
    public string RelicId => RelicIdHelper.Slugify(nameof(Bread));

    public int EnergyGained { get; set; }
    public int EnergyLost { get; set; }

    public string GetDescription(int effectiveTurns, int effectiveCombats)
    {
        return $"Generated {Fmt.Blue(EnergyGained)} [gold]Energy[/gold]. " +
               $"Lost {Fmt.Blue(EnergyLost)} [gold]Energy[/gold] on turn 1.";
    }

    public JsonObject Save()
    {
        var obj = new JsonObject
        {
            ["gained"] = EnergyGained,
            ["lost"] = EnergyLost,
        };
        return obj;
    }

    public void Load(JsonObject data)
    {
        EnergyGained = data["gained"]?.GetValue<int>() ?? 0;
        EnergyLost = data["lost"]?.GetValue<int>() ?? 0;
    }

    public void Reset()
    {
        EnergyGained = 0;
        EnergyLost = 0;
    }

    private static bool TryGet(Bread instance, out BreadStats stats)
    {
        stats = null!;
        if (instance.IsMelted) return false;
        if (!LocalContext.IsMine(instance)) return false;
        if (RelicStatsRegistry.Get(RelicIdHelper.Slugify(nameof(Bread))) is not BreadStats s) return false;
        stats = s;
        return true;
    }

    [HarmonyPatch(typeof(Bread), nameof(Bread.ModifyMaxEnergy))]
    [HarmonyPostfix]
    public static void ModifyMaxEnergyPostfix(Bread __instance, decimal __result, decimal __1)
    {
        int delta = (int)(__result - __1);
        // MaxEnergy is computed, so this also fires on every UI read; count only real grants.
        if (delta <= 0 || !EnergyGrantScope.IsCounting) return;
        if (!TryGet(__instance, out var stats)) return;
        stats.EnergyGained += delta;
    }

    [HarmonyPatch(typeof(Bread), nameof(Bread.AfterSideTurnStart))]
    [HarmonyPostfix]
    public static void AfterSideTurnStartPostfix(Bread __instance, CombatSide side, ICombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (__instance.Owner.PlayerCombatState!.TurnNumber != 1) return;
        if (!TryGet(__instance, out var stats)) return;
        stats.EnergyLost += (int)__instance.DynamicVars["LoseEnergy"].BaseValue;
    }

#if DEBUG
    public void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.SideTurnStart);
        int Lose() => (int)(TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId)?.DynamicVars["LoseEnergy"].BaseValue ?? -1);
        int Gain() => (int)(TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId)?.DynamicVars["GainEnergy"].BaseValue ?? -1);
        // Turn 1: ModifyMaxEnergy returns the amount unchanged and AfterSideTurnStart takes the energy away.
        runner.Assert("turn 1 loses energy, gains none", () =>
        {
            var lose = Lose();
            return new TestResult(lose > 0 && EnergyLost == lose && EnergyGained == 0,
                $"expected lost {lose} / gained 0, got EnergyLost={EnergyLost}, EnergyGained={EnergyGained}");
        });
        // Turn 2: the refill grants the bonus; the turn-2 side start must not take energy again.
        runner.Do("god mode + protect enemy + end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.WaitFor(GameEvent.SideTurnStart, 15000);
        runner.Assert("turn 2 gains energy, loss unchanged", () =>
        {
            var lose = Lose(); var gain = Gain();
            return new TestResult(gain > 0 && EnergyGained == gain && EnergyLost == lose,
                $"expected gained {gain} / lost still {lose}, got EnergyGained={EnergyGained}, EnergyLost={EnergyLost}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// ── Additional simple energy relics ──────────────────────────────────

// Chandelier: gains energy on round 3
[HarmonyPatch(typeof(Chandelier), nameof(Chandelier.AfterSideTurnStart))]
public sealed class ChandelierStats : SimpleCounterStats<Chandelier>
{
    public override string Format => "Generated {0} [gold]Energy[/gold].";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(Chandelier __instance, CombatSide side, ICombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (__instance.Owner.PlayerCombatState!.TurnNumber != 3) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Energy.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("enable god mode + protect enemy", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); });
        // Fires in the turn-3 AfterSideTurnStart. After K EndTurns + PlayerTurnStart, AfterSideTurnStart has
        // run for turns 1..K only: nothing after two EndTurns, the trigger after the third.
        for (int i = 1; i <= 2; i++)
        {
            runner.Do($"end turn {i}", () => TestHelpers.EndTurn());
            runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        }
        runner.Assert("nothing before turn 3", () => new TestResult(Amount == 0, $"expected 0, got {Amount}"));
        runner.Do("end turn 3", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked energy on turn 3", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Energy.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// Candelabra: gains energy on round 2
[HarmonyPatch(typeof(Candelabra), nameof(Candelabra.AfterSideTurnStart))]
public sealed class CandelabraStats : SimpleCounterStats<Candelabra>
{
    public override string Format => "Generated {0} [gold]Energy[/gold].";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(Candelabra __instance, CombatSide side, ICombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (__instance.Owner.PlayerCombatState!.TurnNumber != 2) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Energy.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("enable god mode + protect enemy", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); });
        // Fires in the turn-2 AfterSideTurnStart, which has not run yet at the turn-2 PlayerTurnStart:
        // nothing after one EndTurn, the trigger after the second.
        runner.Do("end turn 1", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("nothing before the turn-2 side start", () => new TestResult(Amount == 0, $"expected 0, got {Amount}"));
        runner.Do("end turn 2", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked energy on turn 2", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Energy.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// VeryHotCocoa: gains energy turn 1
[HarmonyPatch(typeof(VeryHotCocoa), nameof(VeryHotCocoa.AfterSideTurnStart))]
public sealed class VeryHotCocoaStats : SimpleCounterStats<VeryHotCocoa>
{
    public override string Format => "Generated {0} [gold]Energy[/gold].";
    public override StatCadence Cadence => StatCadence.Total;
    public static void Postfix(VeryHotCocoa __instance, CombatSide side, ICombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (__instance.Owner.PlayerCombatState!.TurnNumber > 1) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Energy.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.SideTurnStart);
        int Expected() => TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId)?.DynamicVars.Energy.IntValue ?? -1;
        runner.Assert("tracked energy on turn 1", () =>
        {
            var expected = Expected();
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        // Turn 1 only: the turn-2 side start (after the next PlayerTurnStart) must not count again.
        runner.Do("god mode + protect enemy + end turn", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.WaitFor(GameEvent.SideTurnStart, 15000);
        runner.Assert("still turn-1 energy only", () =>
        {
            var expected = Expected();
            return new TestResult(Amount == expected, $"expected still {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// FakeVenerableTeaSet: gains energy in first combat after rest
[HarmonyPatch(typeof(FakeVenerableTeaSet), nameof(FakeVenerableTeaSet.AfterEnergyReset))]
public sealed class FakeVenerableTeaSetStats : SimpleCounterStats<FakeVenerableTeaSet>
{
    public override string Format => "Generated {0} [gold]Energy[/gold].";
    public override StatCadence Cadence => StatCadence.Total;

    public static void Prefix(FakeVenerableTeaSet __instance, Player player, out bool __state) =>
        __state = __instance.Owner == player && __instance.GainEnergyInNextCombat;

    public static void Postfix(FakeVenerableTeaSet __instance, bool __state)
    {
        if (!__state) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Energy.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        // No rest site yet: the first combat's energy reset must not pay.
        runner.Do("start fight without resting", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("nothing without a rest site", () => new TestResult(Amount == 0, $"expected 0, got {Amount}"));
        // AfterRoomEntered(RestSiteRoom) arms GainEnergyInNextCombat; the next AfterEnergyReset pays it.
        runner.Do("enter rest site", () => TestHelpers.EnterRestSite());
        runner.WaitFor(GameEvent.RoomEntered, 8000);
        runner.Do("start fight after resting", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked energy in the combat after the rest", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Energy.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// VenerableTeaSet: gains energy in first combat after rest
[HarmonyPatch(typeof(VenerableTeaSet), nameof(VenerableTeaSet.AfterEnergyReset))]
public sealed class VenerableTeaSetStats : SimpleCounterStats<VenerableTeaSet>
{
    public override string Format => "Generated {0} [gold]Energy[/gold].";
    public override StatCadence Cadence => StatCadence.Total;

    public static void Prefix(VenerableTeaSet __instance, Player player, out bool __state) =>
        __state = __instance.Owner == player && __instance.GainEnergyInNextCombat;

    public static void Postfix(VenerableTeaSet __instance, bool __state)
    {
        if (!__state) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Energy.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        // No rest site yet: the first combat's energy reset must not pay.
        runner.Do("start fight without resting", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("nothing without a rest site", () => new TestResult(Amount == 0, $"expected 0, got {Amount}"));
        // AfterRoomEntered(RestSiteRoom) arms GainEnergyInNextCombat; the next AfterEnergyReset pays it.
        runner.Do("enter rest site", () => TestHelpers.EnterRestSite());
        runner.WaitFor(GameEvent.RoomEntered, 8000);
        runner.Do("start fight after resting", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked energy in the combat after the rest", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Energy.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// PaelsFlesh: gains energy from round 3+
[HarmonyPatch(typeof(PaelsFlesh), nameof(PaelsFlesh.AfterSideTurnStart))]
public sealed class PaelsFleshStats : SimpleCounterStats<PaelsFlesh>
{
    public override string Format => "Generated {0} [gold]Energy[/gold].";
    public static void Postfix(PaelsFlesh __instance, CombatSide side, ICombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (__instance.Owner.PlayerCombatState!.TurnNumber < 3) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Energy.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("enable god mode + protect enemy", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); });
        // Fires in AfterSideTurnStart from turn 3 on. After K EndTurns + PlayerTurnStart, AfterSideTurnStart
        // has run for turns 1..K only: nothing after two EndTurns, the turn-3 trigger after the third.
        for (int i = 1; i <= 2; i++)
        {
            runner.Do($"end turn {i}", () => TestHelpers.EndTurn());
            runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        }
        runner.Assert("nothing before turn 3", () => new TestResult(Amount == 0, $"expected 0, got {Amount}"));
        runner.Do("end turn 3", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked energy on turn 3", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Energy.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// Ectoplasm: +1 energy, blocks all gold gains
[HarmonyPatch]
public sealed class EctoplasmStats : IRelicStats
{
    public string RelicId => RelicIdHelper.Slugify(nameof(Ectoplasm));

    public int EnergyGenerated { get; set; }
    public int GoldBlocked { get; set; }

    public string GetDescription(int effectiveTurns, int effectiveCombats)
    {
        return $"Generated {Fmt.Blue(EnergyGenerated)} [gold]Energy[/gold]. " +
               $"Blocked {Fmt.Blue(GoldBlocked)} {Fmt.GoldKw}.";
    }

    public JsonObject Save()
    {
        var obj = new JsonObject
        {
            ["energy"] = EnergyGenerated,
            ["goldBlocked"] = GoldBlocked,
        };
        return obj;
    }

    public void Load(JsonObject data)
    {
        EnergyGenerated = data["energy"]?.GetValue<int>() ?? 0;
        GoldBlocked = data["goldBlocked"]?.GetValue<int>() ?? 0;
    }

    public void Reset()
    {
        EnergyGenerated = 0;
        GoldBlocked = 0;
    }

    private static bool TryGet(Ectoplasm instance, out EctoplasmStats stats)
    {
        stats = null!;
        if (instance.IsMelted) return false;
        if (!LocalContext.IsMine(instance)) return false;
        if (RelicStatsRegistry.Get(RelicIdHelper.Slugify(nameof(Ectoplasm))) is not EctoplasmStats s) return false;
        stats = s;
        return true;
    }

    [HarmonyPatch(typeof(Ectoplasm), nameof(Ectoplasm.ModifyMaxEnergy))]
    [HarmonyPostfix]
    public static void ModifyMaxEnergyPostfix(Ectoplasm __instance, decimal __result, decimal __1)
    {
        int delta = (int)(__result - __1);
        // MaxEnergy is computed, so this also fires on every UI read; count only real grants.
        if (delta <= 0 || !EnergyGrantScope.IsCounting) return;
        if (!TryGet(__instance, out var stats)) return;
        stats.EnergyGenerated += delta;
    }

    [HarmonyPatch(typeof(Ectoplasm), nameof(Ectoplasm.ModifyGoldGained))]
    [HarmonyPostfix]
    public static void ModifyGoldGainedPostfix(Ectoplasm __instance, decimal amount, decimal __result, Player player)
    {
        if (player != __instance.Owner) return;
        if (!TryGet(__instance, out var stats)) return;
        stats.GoldBlocked += (int)(amount - __result);
    }

#if DEBUG
    public void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        // Energy is credited at the turn-start refill, which is after CombatStart.
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked energy", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Energy.IntValue ?? -1;
            return new TestResult(expected > 0 && EnergyGenerated == expected, $"expected {expected}, got {EnergyGenerated}");
        });
        // ModifyGoldGained returns 0 for the owner: the whole gain is blocked.
        runner.Do("gain 100 gold", () => TestHelpers.AddGold(100));
        runner.Assert("tracked blocked gold", () => new TestResult(GoldBlocked == 100, $"expected 100, got {GoldBlocked}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// SealOfGold: spends gold for energy each turn
[HarmonyPatch]
public sealed class SealOfGoldStats : IRelicStats
{
    public string RelicId => RelicIdHelper.Slugify(nameof(SealOfGold));

    public int EnergyGenerated { get; set; }
    public int GoldSpent { get; set; }

    public string GetDescription(int effectiveTurns, int effectiveCombats)
    {
        return $"Generated {Fmt.Blue(EnergyGenerated)} [gold]Energy[/gold]. " +
               $"Spent {Fmt.Blue(GoldSpent)} {Fmt.GoldKw}.";
    }

    public JsonObject Save()
    {
        var obj = new JsonObject
        {
            ["energy"] = EnergyGenerated,
            ["goldSpent"] = GoldSpent,
        };
        return obj;
    }

    public void Load(JsonObject data)
    {
        EnergyGenerated = data["energy"]?.GetValue<int>() ?? 0;
        GoldSpent = data["goldSpent"]?.GetValue<int>() ?? 0;
    }

    public void Reset()
    {
        EnergyGenerated = 0;
        GoldSpent = 0;
    }

    private static bool TryGet(SealOfGold instance, out SealOfGoldStats stats)
    {
        stats = null!;
        if (instance.IsMelted) return false;
        if (!LocalContext.IsMine(instance)) return false;
        if (RelicStatsRegistry.Get(RelicIdHelper.Slugify(nameof(SealOfGold))) is not SealOfGoldStats s) return false;
        stats = s;
        return true;
    }

    // The game's AfterSideTurnStart gains the energy and then LoseGold(3) synchronously, so a Postfix
    // reads the gold AFTER it was spent and wrongly skips at exactly 3-5 gold. Capture it beforehand.
    [HarmonyPatch(typeof(SealOfGold), nameof(SealOfGold.AfterSideTurnStart))]
    [HarmonyPrefix]
    public static void AfterSideTurnStartPrefix(SealOfGold __instance, out int __state) =>
        __state = __instance.Owner.Gold;

    [HarmonyPatch(typeof(SealOfGold), nameof(SealOfGold.AfterSideTurnStart))]
    [HarmonyPostfix]
    public static void AfterSideTurnStartPostfix(SealOfGold __instance, IReadOnlyList<Creature> participants, int __state)
    {
        if (!participants.Contains(__instance.Owner.Creature)) return;
        if (__state < __instance.DynamicVars.Gold.IntValue) return;
        if (!TryGet(__instance, out var stats)) return;
        stats.EnergyGenerated += __instance.DynamicVars.Energy.IntValue;
        stats.GoldSpent += __instance.DynamicVars.Gold.IntValue;
    }

#if DEBUG
    public void RegisterTest(TestRunner runner)
    {
        int goldBefore = 0;
        int Energy() => TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId)?.DynamicVars.Energy.IntValue ?? -1;
        int Gold() => TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId)?.DynamicVars.Gold.IntValue ?? -1;
        runner.Do("add relic + give gold", () =>
        {
            TestHelpers.AddRelic(RelicId);
            TestHelpers.AddGold(999);
        });
        runner.Do("start fight", () => { goldBefore = TestHelpers.Player!.Gold; TestHelpers.StartFight(); });
        runner.WaitFor(GameEvent.SideTurnStart);
        runner.Assert("tracked energy and gold spent on turn 1", () =>
        {
            var energy = Energy(); var gold = Gold();
            int spent = goldBefore - TestHelpers.Player!.Gold;
            return new TestResult(energy > 0 && gold > 0 && EnergyGenerated == energy && GoldSpent == gold && spent == gold,
                $"expected energy {energy} / gold {gold} (player spent {spent}), got EnergyGenerated={EnergyGenerated}, GoldSpent={GoldSpent}");
        });
        // Exactly the price: the relic still fires, and the old post-spend read (0 < 3) would have missed it.
        runner.Do("set gold to the exact price + end turn", () =>
        {
            TestHelpers.SetGold(Gold());
            TestHelpers.EnableGodMode();
            TestHelpers.ProtectEnemy();
            TestHelpers.EndTurn();
        });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.WaitFor(GameEvent.SideTurnStart, 15000);
        runner.Assert("fires with exactly the price in gold", () =>
        {
            var energy = 2 * Energy(); var gold = 2 * Gold();
            return new TestResult(EnergyGenerated == energy && GoldSpent == gold && TestHelpers.Player!.Gold == 0,
                $"expected energy {energy} / gold {gold} (player gold {TestHelpers.Player!.Gold}), got EnergyGenerated={EnergyGenerated}, GoldSpent={GoldSpent}");
        });
        // Below the price: nothing.
        runner.Do("set gold to 0 + end turn", () => { TestHelpers.SetGold(0); TestHelpers.EndTurn(); });
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.WaitFor(GameEvent.SideTurnStart, 15000);
        runner.Assert("nothing without enough gold", () =>
        {
            var energy = 2 * Energy(); var gold = 2 * Gold();
            return new TestResult(EnergyGenerated == energy && GoldSpent == gold,
                $"expected still energy {energy} / gold {gold}, got EnergyGenerated={EnergyGenerated}, GoldSpent={GoldSpent}");
        });
        runner.Cleanup(() => { TestHelpers.EnableGodMode(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// Sozu: +1 energy, blocks potion procurement
[HarmonyPatch]
public sealed class SozuStats : IRelicStats
{
    public string RelicId => RelicIdHelper.Slugify(nameof(Sozu));

    public int EnergyGenerated { get; set; }
    public int PotionsBlocked { get; set; }

    public string GetDescription(int effectiveTurns, int effectiveCombats)
    {
        return $"Generated {Fmt.Blue(EnergyGenerated)} [gold]Energy[/gold]. " +
               $"Blocked {Fmt.Blue(PotionsBlocked)} potions.";
    }

    public JsonObject Save()
    {
        var obj = new JsonObject
        {
            ["energy"] = EnergyGenerated,
            ["potionsBlocked"] = PotionsBlocked,
        };
        return obj;
    }

    public void Load(JsonObject data)
    {
        EnergyGenerated = data["energy"]?.GetValue<int>() ?? 0;
        PotionsBlocked = data["potionsBlocked"]?.GetValue<int>() ?? 0;
    }

    public void Reset()
    {
        EnergyGenerated = 0;
        PotionsBlocked = 0;
    }

    private static bool TryGet(Sozu instance, out SozuStats stats)
    {
        stats = null!;
        if (instance.IsMelted) return false;
        if (!LocalContext.IsMine(instance)) return false;
        if (RelicStatsRegistry.Get(RelicIdHelper.Slugify(nameof(Sozu))) is not SozuStats s) return false;
        stats = s;
        return true;
    }

    [HarmonyPatch(typeof(Sozu), nameof(Sozu.ModifyMaxEnergy))]
    [HarmonyPostfix]
    public static void ModifyMaxEnergyPostfix(Sozu __instance, decimal __result, decimal __1)
    {
        int delta = (int)(__result - __1);
        // MaxEnergy is computed, so this also fires on every UI read; count only real grants.
        if (delta <= 0 || !EnergyGrantScope.IsCounting) return;
        if (!TryGet(__instance, out var stats)) return;
        stats.EnergyGenerated += delta;
    }

    [HarmonyPatch(typeof(Sozu), nameof(Sozu.ShouldProcurePotion))]
    [HarmonyPostfix]
    public static void ShouldProcurePotionPostfix(Sozu __instance, Player player)
    {
        if (player != __instance.Owner) return;
        if (!TryGet(__instance, out var stats)) return;
        stats.PotionsBlocked++;
    }

#if DEBUG
    public void RegisterTest(TestRunner runner)
    {
        // Potions persist across tests, so start from an empty belt for the held-potion checks below.
        runner.Do("add relic + clear potions", () => { TestHelpers.AddRelic(RelicId); TestHelpers.ClearPotions(); });
        runner.Do("start fight", () => TestHelpers.StartFight());
        // Energy is credited at the turn-start refill, which is after CombatStart.
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked energy", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Energy.IntValue ?? -1;
            return new TestResult(expected > 0 && EnergyGenerated == expected, $"expected {expected}, got {EnergyGenerated}");
        });
        // PotionCmd.TryToProcure asks Hook.ShouldProcurePotion first; Sozu says no, so the potion never lands.
        runner.Do("try to obtain a potion", () => TestHelpers.AddPotion("FLEX_POTION"));
        runner.Assert("tracked the blocked potion", () =>
        {
            bool none = !TestHelpers.Player!.Potions.Any();
            return new TestResult(PotionsBlocked == 1 && none, $"expected 1 blocked and no potions (none: {none}), got PotionsBlocked={PotionsBlocked}");
        });
        // Without the relic the potion is procured and nothing more is counted.
        runner.Do("remove relic + obtain a potion", () => { TestHelpers.RemoveRelic(RelicId); TestHelpers.AddPotion("FLEX_POTION"); });
        runner.Assert("no count without the relic", () =>
        {
            bool one = TestHelpers.Player!.Potions.Count() == 1;
            return new TestResult(PotionsBlocked == 1 && one, $"expected still 1 with one potion held (held one: {one}), got PotionsBlocked={PotionsBlocked}");
        });
        runner.Cleanup(() => { TestHelpers.ClearPotions(); TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// SpikedGauntlets: +1 energy, powers cost 1 more
[HarmonyPatch]
public sealed class SpikedGauntletsStats : IRelicStats
{
    public string RelicId => RelicIdHelper.Slugify(nameof(SpikedGauntlets));

    public int EnergyGenerated { get; set; }
    public int PowerCostIncrease { get; set; }

    public string GetDescription(int effectiveTurns, int effectiveCombats)
    {
        return $"Generated {Fmt.Blue(EnergyGenerated)} [gold]Energy[/gold]. " +
               $"Increased power costs {Fmt.Blue(PowerCostIncrease)} times.";
    }

    public JsonObject Save()
    {
        var obj = new JsonObject
        {
            ["energy"] = EnergyGenerated,
            ["powerCost"] = PowerCostIncrease,
        };
        return obj;
    }

    public void Load(JsonObject data)
    {
        EnergyGenerated = data["energy"]?.GetValue<int>() ?? 0;
        PowerCostIncrease = data["powerCost"]?.GetValue<int>() ?? 0;
    }

    public void Reset()
    {
        EnergyGenerated = 0;
        PowerCostIncrease = 0;
    }

    private static bool TryGet(SpikedGauntlets instance, out SpikedGauntletsStats stats)
    {
        stats = null!;
        if (instance.IsMelted) return false;
        if (!LocalContext.IsMine(instance)) return false;
        if (RelicStatsRegistry.Get(RelicIdHelper.Slugify(nameof(SpikedGauntlets))) is not SpikedGauntletsStats s) return false;
        stats = s;
        return true;
    }

    [HarmonyPatch(typeof(SpikedGauntlets), nameof(SpikedGauntlets.ModifyMaxEnergy))]
    [HarmonyPostfix]
    public static void ModifyMaxEnergyPostfix(SpikedGauntlets __instance, decimal __result, decimal __1)
    {
        int delta = (int)(__result - __1);
        // MaxEnergy is computed, so this also fires on every UI read; count only real grants.
        if (delta <= 0 || !EnergyGrantScope.IsCounting) return;
        if (!TryGet(__instance, out var stats)) return;
        stats.EnergyGenerated += delta;
    }

    // TryModifyEnergyCostInCombat runs on every cost read (hand rendering, hover previews), so counting
    // there over-counts massively. Count the surcharge once per Power actually played and paid for.
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardPlayed))]
    [HarmonyPostfix]
    public static void AfterCardPlayedPostfix(CardPlay cardPlay)
    {
        if (cardPlay.Card.Type != CardType.Power) return;
        // Auto-plays and Replay repeats do not pay energy, so the surcharge never applied to them.
        if (cardPlay.IsAutoPlay || cardPlay.PlayIndex != 0) return;
        var relic = cardPlay.Card.Owner?.GetRelic<SpikedGauntlets>();
        if (relic == null) return;
        if (!TryGet(relic, out var stats)) return;
        stats.PowerCostIncrease++;
    }

#if DEBUG
    public void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        // Energy is credited at the turn-start refill, which is after CombatStart.
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked energy", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Energy.IntValue ?? -1;
            return new TestResult(expected > 0 && EnergyGenerated == expected, $"expected {expected}, got {EnergyGenerated}");
        });
        // One Power played = one surcharge paid (Demon Form costs 3 + 1 here).
        runner.Do("play a power", () =>
        {
            TestHelpers.AddEnergy(10);
            TestHelpers.SpawnCard("DEMON_FORM");
            TestHelpers.PlayCard(0);
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("tracked one power surcharge", () => new TestResult(PowerCostIncrease == 1, $"expected 1, got {PowerCostIncrease}"));
        // A Power sitting in hand gets its cost read for rendering but is never paid for, and a played
        // Skill is not a Power: neither may count.
        runner.Do("leave a power in hand, play a skill", () =>
        {
            TestHelpers.SpawnCard("DEMON_FORM");
            TestHelpers.SpawnCard("DEFEND_IRONCLAD");
            TestHelpers.PlayCard(TestHelpers.FindCardInHand(CardType.Skill));
        });
        runner.WaitFor(GameEvent.CardPlayed);
        runner.Assert("still one surcharge", () => new TestResult(PowerCostIncrease == 1, $"expected still 1, got {PowerCostIncrease}"));
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}
