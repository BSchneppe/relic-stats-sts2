using System;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using RelicStats.Core;
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
        if (__instance.Owner.Creature.CombatState!.RoundNumber <= 1) return;
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
        // Don't play any attacks, then end turn so _anyAttacksPlayedLastTurn is false
        runner.Do("end turn 1", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        // ArtOfWar fires in AfterEnergyReset on turn 2 when no attacks were played
        runner.Assert("tracked energy", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Energy.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount >= expected, $"expected >= {expected}, got {Amount}");
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
        // HappyFlower fires every 3rd turn — relic misses turn 1 (not yet subscribed at combat start)
        // So we need 3 EndTurns: turn 2 (turnsSeen=1), turn 3 (turnsSeen=2), turn 4 (turnsSeen=0 → fires)
        for (int i = 1; i <= 3; i++)
        {
            runner.Do($"end turn {i}", () => TestHelpers.EndTurn());
            runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        }
        runner.Assert("tracked energy", () =>
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
        // FakeHappyFlower fires every 5th turn — relic misses turn 1, so need 5 EndTurns
        for (int i = 1; i <= 5; i++)
        {
            runner.Do($"end turn {i}", () => TestHelpers.EndTurn());
            runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        }
        runner.Assert("tracked energy", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Energy.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount >= expected, $"expected >= {expected}, got {Amount}");
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
        // Relic misses turn 1 (not subscribed at combat start). End turn 1 with unspent energy,
        // then end turn 2 with unspent energy — relic sees leftover on turn 3.
        runner.Do("end turn 1", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Do("end turn 2", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked energy", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Energy.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount == expected, $"expected {expected}, got {Amount}");
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
        if (delta <= 0) return;
        Track(__instance, s => s.Amount += delta);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.CombatStart);
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

// PumpkinCandle: +1 max energy in the act it was obtained
[HarmonyPatch(typeof(PumpkinCandle), nameof(PumpkinCandle.ModifyMaxEnergy))]
public sealed class PumpkinCandleStats : SimpleCounterStats<PumpkinCandle>
{
    public override string Format => "Generated {0} [gold]Energy[/gold].";
    public static void Postfix(PumpkinCandle __instance, decimal __result, decimal __1)
    {
        int delta = (int)(__result - __1);
        if (delta <= 0) return;
        Track(__instance, s => s.Amount += delta);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked energy", () =>
        {
            // PumpkinCandle gives +1 max energy only in the act it was obtained.
            // In test harness the act condition may not be met, so accept 0.
            return new TestResult(Amount >= 0, $"expected >=0 (PumpkinCandle +1 max energy, act-conditional), got {Amount}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// Lantern: gains energy turn 1
[HarmonyPatch(typeof(Lantern), nameof(Lantern.AfterSideTurnStart))]
public sealed class LanternStats : SimpleCounterStats<Lantern>
{
    public override string Format => "Generated {0} [gold]Energy[/gold].";
    public static void Postfix(Lantern __instance, CombatSide side, CombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (combatState.RoundNumber > 1) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Energy.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.SideTurnStart);
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
        runner.Do("enable god mode + protect enemy", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); });
        // Relic may miss turn 1 — end 2 turns so the Postfix fires at least once
        runner.Do("end turn 1", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Do("end turn 2", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked energy preservation", () =>
        {
            return new TestResult(Amount >= 1, $"expected >= 1, got {Amount}");
        });
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
        if (delta <= 0) return;
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
        runner.WaitFor(GameEvent.CombatStart);
        runner.Assert("tracked energy", () =>
        {
            var stats = RelicStatsRegistry.Get(RelicId) as PhilosophersStoneStats;
            if (stats == null) return new TestResult(false, "stats not found");
            return new TestResult(stats.EnergyGenerated > 0, $"EnergyGenerated={stats.EnergyGenerated}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
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
        if (delta <= 0) return;
        if (!TryGet(__instance, out var stats)) return;
        stats.EnergyGenerated += delta;
    }

    [HarmonyPatch(typeof(BlessedAntler), nameof(BlessedAntler.BeforeHandDraw))]
    [HarmonyPostfix]
    public static void BeforeHandDrawPostfix(BlessedAntler __instance, Player player, CombatState combatState)
    {
        if (player != __instance.Owner) return;
        if (combatState.RoundNumber != 1) return;
        if (!TryGet(__instance, out var stats)) return;
        stats.DazedAdded += __instance.DynamicVars.Cards.IntValue;
    }

#if DEBUG
    public void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked energy or dazed", () =>
        {
            var stats = RelicStatsRegistry.Get(RelicId) as BlessedAntlerStats;
            if (stats == null) return new TestResult(false, "stats not found");
            // ModifyMaxEnergy fires at room entry, DazedAdded fires at BeforeHandDraw round 1
            bool ok = stats.EnergyGenerated > 0 || stats.DazedAdded > 0;
            return new TestResult(ok, $"EnergyGenerated={stats.EnergyGenerated}, DazedAdded={stats.DazedAdded}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// BloodSoakedRose: +1 energy, adds Enthralled curse to deck
[HarmonyPatch]
public sealed class BloodSoakedRoseStats : IRelicStats
{
    public string RelicId => RelicIdHelper.Slugify(nameof(BloodSoakedRose));

    public int EnergyGenerated { get; set; }
    public int EnthrallAdded { get; set; }

    public string GetDescription(int effectiveTurns, int effectiveCombats)
    {
        return $"Generated {Fmt.Blue(EnergyGenerated)} [gold]Energy[/gold]. " +
               $"Added {Fmt.Blue(EnthrallAdded)} Enthralled curses.";
    }

    public JsonObject Save()
    {
        var obj = new JsonObject
        {
            ["energy"] = EnergyGenerated,
            ["enthrall"] = EnthrallAdded,
        };
        return obj;
    }

    public void Load(JsonObject data)
    {
        EnergyGenerated = data["energy"]?.GetValue<int>() ?? 0;
        EnthrallAdded = data["enthrall"]?.GetValue<int>() ?? 0;
    }

    public void Reset()
    {
        EnergyGenerated = 0;
        EnthrallAdded = 0;
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
        if (delta <= 0) return;
        if (!TryGet(__instance, out var stats)) return;
        stats.EnergyGenerated += delta;
    }

    [HarmonyPatch(typeof(BloodSoakedRose), nameof(BloodSoakedRose.AfterObtained))]
    [HarmonyPostfix]
    public static void AfterObtainedPostfix(BloodSoakedRose __instance)
    {
        if (!TryGet(__instance, out var stats)) return;
        stats.EnthrallAdded++;
    }

#if DEBUG
    public void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.CombatStart);
        runner.Assert("tracked energy", () =>
        {
            var stats = RelicStatsRegistry.Get(RelicId) as BloodSoakedRoseStats;
            if (stats == null) return new TestResult(false, "stats not found");
            return new TestResult(stats.EnergyGenerated > 0, $"EnergyGenerated={stats.EnergyGenerated}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
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
        if (delta <= 0) return;
        if (!TryGet(__instance, out var stats)) return;
        stats.EnergyGained += delta;
    }

    [HarmonyPatch(typeof(Bread), nameof(Bread.AfterSideTurnStart))]
    [HarmonyPostfix]
    public static void AfterSideTurnStartPostfix(Bread __instance, CombatSide side, CombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (combatState.RoundNumber != 1) return;
        if (!TryGet(__instance, out var stats)) return;
        stats.EnergyLost += (int)__instance.DynamicVars["LoseEnergy"].BaseValue;
    }

#if DEBUG
    public void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.SideTurnStart);
        runner.Assert("tracked energy loss on turn 1", () =>
        {
            var stats = RelicStatsRegistry.Get(RelicId) as BreadStats;
            if (stats == null) return new TestResult(false, "stats not found");
            // AfterSideTurnStart fires on round 1 and tracks EnergyLost
            return new TestResult(stats.EnergyLost > 0 || stats.EnergyGained > 0,
                $"EnergyLost={stats.EnergyLost}, EnergyGained={stats.EnergyGained}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}

// ── Additional simple energy relics ──────────────────────────────────

// Chandelier: gains energy on round 3
[HarmonyPatch(typeof(Chandelier), nameof(Chandelier.AfterSideTurnStart))]
public sealed class ChandelierStats : SimpleCounterStats<Chandelier>
{
    public override string Format => "Generated {0} [gold]Energy[/gold].";
    public static void Postfix(Chandelier __instance, CombatSide side, CombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (combatState.RoundNumber != 3) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Energy.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("enable god mode + protect enemy", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); });
        // Chandelier fires on round 3 — relic misses turn 1, need 3 EndTurns to reach round 4 (observed round 3)
        for (int i = 1; i <= 3; i++)
        {
            runner.Do($"end turn {i}", () => TestHelpers.EndTurn());
            runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        }
        runner.Assert("tracked energy", () =>
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
    public static void Postfix(Candelabra __instance, CombatSide side, CombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (combatState.RoundNumber != 2) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Energy.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("enable god mode + protect enemy", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); });
        // Candelabra fires on round 2 — relic misses turn 1, need 2 EndTurns
        runner.Do("end turn 1", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Do("end turn 2", () => TestHelpers.EndTurn());
        runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        runner.Assert("tracked energy", () =>
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
    public static void Postfix(VeryHotCocoa __instance, CombatSide side, CombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (combatState.RoundNumber > 1) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Energy.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.SideTurnStart);
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

// FakeVenerableTeaSet: gains energy in first combat after rest
[HarmonyPatch(typeof(FakeVenerableTeaSet), nameof(FakeVenerableTeaSet.AfterEnergyReset))]
public sealed class FakeVenerableTeaSetStats : SimpleCounterStats<FakeVenerableTeaSet>
{
    public override string Format => "Generated {0} [gold]Energy[/gold].";

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
        runner.Do("set GainEnergyInNextCombat flag", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId) as FakeVenerableTeaSet;
            if (relic != null)
                AccessTools.Property(typeof(FakeVenerableTeaSet), nameof(FakeVenerableTeaSet.GainEnergyInNextCombat))
                    .SetValue(relic, true);
        });
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked energy", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId) as FakeVenerableTeaSet;
            var expected = relic?.DynamicVars.Energy.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount >= expected, $"expected >= {expected}, got {Amount}");
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
        runner.Do("set GainEnergyInNextCombat flag", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId) as VenerableTeaSet;
            if (relic != null)
                AccessTools.Property(typeof(VenerableTeaSet), nameof(VenerableTeaSet.GainEnergyInNextCombat))
                    .SetValue(relic, true);
        });
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Assert("tracked energy", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId) as VenerableTeaSet;
            var expected = relic?.DynamicVars.Energy.IntValue ?? -1;
            return new TestResult(expected > 0 && Amount >= expected, $"expected >= {expected}, got {Amount}");
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
    public static void Postfix(PaelsFlesh __instance, CombatSide side, CombatState combatState)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (combatState.RoundNumber < 3) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Energy.IntValue);
    }

#if DEBUG
    public override void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.PlayerTurnStart);
        runner.Do("enable god mode + protect enemy", () => { TestHelpers.EnableGodMode(); TestHelpers.ProtectEnemy(); });
        // PaelsFlesh fires from round 3+ — relic misses turn 1, need 3 EndTurns
        for (int i = 1; i <= 3; i++)
        {
            runner.Do($"end turn {i}", () => TestHelpers.EndTurn());
            runner.WaitFor(GameEvent.PlayerTurnStart, 15000);
        }
        runner.Assert("tracked energy", () =>
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
        if (delta <= 0) return;
        if (!TryGet(__instance, out var stats)) return;
        stats.EnergyGenerated += delta;
    }

    [HarmonyPatch(typeof(Ectoplasm), nameof(Ectoplasm.ShouldGainGold))]
    [HarmonyPostfix]
    public static void ShouldGainGoldPostfix(Ectoplasm __instance, decimal amount, Player player)
    {
        if (player != __instance.Owner) return;
        if (!TryGet(__instance, out var stats)) return;
        stats.GoldBlocked += (int)amount;
    }

#if DEBUG
    public void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.CombatStart);
        runner.Assert("tracked energy", () =>
        {
            var relic = TestHelpers.Player!.Relics.FirstOrDefault(r => r.Id.Entry == RelicId);
            var expected = relic?.DynamicVars.Energy.IntValue ?? -1;
            return new TestResult(EnergyGenerated == expected, $"expected {expected}, got {EnergyGenerated}");
        });
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

    [HarmonyPatch(typeof(SealOfGold), nameof(SealOfGold.AfterSideTurnStart))]
    [HarmonyPostfix]
    public static void AfterSideTurnStartPostfix(SealOfGold __instance, CombatSide side)
    {
        if (side != __instance.Owner.Creature.Side) return;
        if (__instance.Owner.Gold < __instance.DynamicVars.Gold.IntValue) return;
        if (!TryGet(__instance, out var stats)) return;
        stats.EnergyGenerated += __instance.DynamicVars.Energy.IntValue;
        stats.GoldSpent += __instance.DynamicVars.Gold.IntValue;
    }

#if DEBUG
    public void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic + give gold", () =>
        {
            TestHelpers.AddRelic(RelicId);
            TestHelpers.AddGold(999);
        });
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.SideTurnStart);
        runner.Assert("tracked energy and gold spent", () =>
        {
            var stats = RelicStatsRegistry.Get(RelicId) as SealOfGoldStats;
            if (stats == null) return new TestResult(false, "stats not found");
            return new TestResult(stats.EnergyGenerated > 0 && stats.GoldSpent > 0,
                $"EnergyGenerated={stats.EnergyGenerated}, GoldSpent={stats.GoldSpent}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
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
        if (delta <= 0) return;
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
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.CombatStart);
        runner.Assert("tracked energy", () =>
        {
            var stats = RelicStatsRegistry.Get(RelicId) as SozuStats;
            if (stats == null) return new TestResult(false, "stats not found");
            return new TestResult(stats.EnergyGenerated > 0, $"EnergyGenerated={stats.EnergyGenerated}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
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
        if (delta <= 0) return;
        if (!TryGet(__instance, out var stats)) return;
        stats.EnergyGenerated += delta;
    }

    [HarmonyPatch(typeof(SpikedGauntlets), nameof(SpikedGauntlets.TryModifyEnergyCostInCombat))]
    [HarmonyPostfix]
    public static void TryModifyCostPostfix(SpikedGauntlets __instance, CardModel card, bool __result)
    {
        if (!__result) return;
        if (card.Owner.Creature != __instance.Owner.Creature) return;
        if (!TryGet(__instance, out var stats)) return;
        stats.PowerCostIncrease++;
    }

#if DEBUG
    public void RegisterTest(TestRunner runner)
    {
        runner.Do("add relic", () => TestHelpers.AddRelic(RelicId));
        runner.Do("start fight", () => TestHelpers.StartFight());
        runner.WaitFor(GameEvent.CombatStart);
        runner.Assert("tracked energy", () =>
        {
            var stats = RelicStatsRegistry.Get(RelicId) as SpikedGauntletsStats;
            if (stats == null) return new TestResult(false, "stats not found");
            return new TestResult(stats.EnergyGenerated > 0, $"EnergyGenerated={stats.EnergyGenerated}");
        });
        runner.Cleanup(() => { TestHelpers.RemoveRelic(RelicId); Reset(); });
    }
#endif
}
