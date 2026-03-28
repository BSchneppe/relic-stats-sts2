using System;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using RelicStats.Core;

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
        if (__instance.Owner.Creature.CombatState.RoundNumber <= 1) return;
        _hadNoAttacks = !(bool)AttacksField.GetValue(__instance)!;
    }

    public static void Postfix(ArtOfWar __instance, Player player)
    {
        if (!_hadNoAttacks) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Energy.IntValue);
    }
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
        if (side != __instance.Owner.Creature.Side) return;
        var turnsSeen = (int)TurnsSeenField.GetValue(__instance)!;
        if (turnsSeen != 0) return;
        Track(__instance, s => s.Amount += __instance.DynamicVars.Energy.IntValue);
    }
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
}

// ── Complex energy relics ─────────────────────────────────────────────

// PhilosophersStone: +1 energy, gives enemies strength
[HarmonyPatch]
public sealed class PhilosophersStoneStats : IRelicStats
{
    public string RelicId => RelicIdHelper.Slugify(nameof(PhilosophersStone));
    public int TurnWhenObtained { get; set; }
    public int CombatWhenObtained { get; set; }
    public int? FrozenTurnCount { get; set; }
    public int? FrozenCombatCount { get; set; }

    public int EnergyGenerated { get; set; }
    public int StrengthGiven { get; set; }

    public string GetDescription(int totalTurns, int totalCombats)
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
            ["turnObtained"] = TurnWhenObtained,
            ["combatObtained"] = CombatWhenObtained,
        };
        if (FrozenTurnCount.HasValue) obj["frozenTurns"] = FrozenTurnCount.Value;
        if (FrozenCombatCount.HasValue) obj["frozenCombats"] = FrozenCombatCount.Value;
        return obj;
    }

    public void Load(JsonObject data)
    {
        EnergyGenerated = data["energy"]?.GetValue<int>() ?? 0;
        StrengthGiven = data["strength"]?.GetValue<int>() ?? 0;
        TurnWhenObtained = data["turnObtained"]?.GetValue<int>() ?? 0;
        CombatWhenObtained = data["combatObtained"]?.GetValue<int>() ?? 0;
        FrozenTurnCount = data["frozenTurns"]?.GetValue<int>();
        FrozenCombatCount = data["frozenCombats"]?.GetValue<int>();
    }

    public void Reset()
    {
        EnergyGenerated = 0;
        StrengthGiven = 0;
        TurnWhenObtained = RelicStatsRegistry.TurnCount;
        CombatWhenObtained = RelicStatsRegistry.CombatCount;
        FrozenTurnCount = null;
        FrozenCombatCount = null;
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
        int enemies = __instance.Owner.Creature.CombatState
            .GetOpponentsOf(__instance.Owner.Creature)
            .Count(c => c.IsAlive);
        stats.StrengthGiven += (int)__instance.DynamicVars["StrengthPower"].BaseValue * enemies;
    }
}

// BlessedAntler: +1 energy, adds Dazed cards turn 1
[HarmonyPatch]
public sealed class BlessedAntlerStats : IRelicStats
{
    public string RelicId => RelicIdHelper.Slugify(nameof(BlessedAntler));
    public int TurnWhenObtained { get; set; }
    public int CombatWhenObtained { get; set; }
    public int? FrozenTurnCount { get; set; }
    public int? FrozenCombatCount { get; set; }

    public int EnergyGenerated { get; set; }
    public int DazedAdded { get; set; }

    public string GetDescription(int totalTurns, int totalCombats)
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
            ["turnObtained"] = TurnWhenObtained,
            ["combatObtained"] = CombatWhenObtained,
        };
        if (FrozenTurnCount.HasValue) obj["frozenTurns"] = FrozenTurnCount.Value;
        if (FrozenCombatCount.HasValue) obj["frozenCombats"] = FrozenCombatCount.Value;
        return obj;
    }

    public void Load(JsonObject data)
    {
        EnergyGenerated = data["energy"]?.GetValue<int>() ?? 0;
        DazedAdded = data["dazed"]?.GetValue<int>() ?? 0;
        TurnWhenObtained = data["turnObtained"]?.GetValue<int>() ?? 0;
        CombatWhenObtained = data["combatObtained"]?.GetValue<int>() ?? 0;
        FrozenTurnCount = data["frozenTurns"]?.GetValue<int>();
        FrozenCombatCount = data["frozenCombats"]?.GetValue<int>();
    }

    public void Reset()
    {
        EnergyGenerated = 0;
        DazedAdded = 0;
        TurnWhenObtained = RelicStatsRegistry.TurnCount;
        CombatWhenObtained = RelicStatsRegistry.CombatCount;
        FrozenTurnCount = null;
        FrozenCombatCount = null;
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
}

// BloodSoakedRose: +1 energy, adds Enthralled curse to deck
[HarmonyPatch]
public sealed class BloodSoakedRoseStats : IRelicStats
{
    public string RelicId => RelicIdHelper.Slugify(nameof(BloodSoakedRose));
    public int TurnWhenObtained { get; set; }
    public int CombatWhenObtained { get; set; }
    public int? FrozenTurnCount { get; set; }
    public int? FrozenCombatCount { get; set; }

    public int EnergyGenerated { get; set; }
    public int EnthrallAdded { get; set; }

    public string GetDescription(int totalTurns, int totalCombats)
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
            ["turnObtained"] = TurnWhenObtained,
            ["combatObtained"] = CombatWhenObtained,
        };
        if (FrozenTurnCount.HasValue) obj["frozenTurns"] = FrozenTurnCount.Value;
        if (FrozenCombatCount.HasValue) obj["frozenCombats"] = FrozenCombatCount.Value;
        return obj;
    }

    public void Load(JsonObject data)
    {
        EnergyGenerated = data["energy"]?.GetValue<int>() ?? 0;
        EnthrallAdded = data["enthrall"]?.GetValue<int>() ?? 0;
        TurnWhenObtained = data["turnObtained"]?.GetValue<int>() ?? 0;
        CombatWhenObtained = data["combatObtained"]?.GetValue<int>() ?? 0;
        FrozenTurnCount = data["frozenTurns"]?.GetValue<int>();
        FrozenCombatCount = data["frozenCombats"]?.GetValue<int>();
    }

    public void Reset()
    {
        EnergyGenerated = 0;
        EnthrallAdded = 0;
        TurnWhenObtained = RelicStatsRegistry.TurnCount;
        CombatWhenObtained = RelicStatsRegistry.CombatCount;
        FrozenTurnCount = null;
        FrozenCombatCount = null;
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
}

// Bread: loses energy turn 1, gains energy other turns
[HarmonyPatch]
public sealed class BreadStats : IRelicStats
{
    public string RelicId => RelicIdHelper.Slugify(nameof(Bread));
    public int TurnWhenObtained { get; set; }
    public int CombatWhenObtained { get; set; }
    public int? FrozenTurnCount { get; set; }
    public int? FrozenCombatCount { get; set; }

    public int EnergyGained { get; set; }
    public int EnergyLost { get; set; }

    public string GetDescription(int totalTurns, int totalCombats)
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
            ["turnObtained"] = TurnWhenObtained,
            ["combatObtained"] = CombatWhenObtained,
        };
        if (FrozenTurnCount.HasValue) obj["frozenTurns"] = FrozenTurnCount.Value;
        if (FrozenCombatCount.HasValue) obj["frozenCombats"] = FrozenCombatCount.Value;
        return obj;
    }

    public void Load(JsonObject data)
    {
        EnergyGained = data["gained"]?.GetValue<int>() ?? 0;
        EnergyLost = data["lost"]?.GetValue<int>() ?? 0;
        TurnWhenObtained = data["turnObtained"]?.GetValue<int>() ?? 0;
        CombatWhenObtained = data["combatObtained"]?.GetValue<int>() ?? 0;
        FrozenTurnCount = data["frozenTurns"]?.GetValue<int>();
        FrozenCombatCount = data["frozenCombats"]?.GetValue<int>();
    }

    public void Reset()
    {
        EnergyGained = 0;
        EnergyLost = 0;
        TurnWhenObtained = RelicStatsRegistry.TurnCount;
        CombatWhenObtained = RelicStatsRegistry.CombatCount;
        FrozenTurnCount = null;
        FrozenCombatCount = null;
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
}

// Ectoplasm: +1 energy, blocks all gold gains
[HarmonyPatch]
public sealed class EctoplasmStats : IRelicStats
{
    public string RelicId => RelicIdHelper.Slugify(nameof(Ectoplasm));
    public int TurnWhenObtained { get; set; }
    public int CombatWhenObtained { get; set; }
    public int? FrozenTurnCount { get; set; }
    public int? FrozenCombatCount { get; set; }

    public int EnergyGenerated { get; set; }
    public int GoldBlocked { get; set; }

    public string GetDescription(int totalTurns, int totalCombats)
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
            ["turnObtained"] = TurnWhenObtained,
            ["combatObtained"] = CombatWhenObtained,
        };
        if (FrozenTurnCount.HasValue) obj["frozenTurns"] = FrozenTurnCount.Value;
        if (FrozenCombatCount.HasValue) obj["frozenCombats"] = FrozenCombatCount.Value;
        return obj;
    }

    public void Load(JsonObject data)
    {
        EnergyGenerated = data["energy"]?.GetValue<int>() ?? 0;
        GoldBlocked = data["goldBlocked"]?.GetValue<int>() ?? 0;
        TurnWhenObtained = data["turnObtained"]?.GetValue<int>() ?? 0;
        CombatWhenObtained = data["combatObtained"]?.GetValue<int>() ?? 0;
        FrozenTurnCount = data["frozenTurns"]?.GetValue<int>();
        FrozenCombatCount = data["frozenCombats"]?.GetValue<int>();
    }

    public void Reset()
    {
        EnergyGenerated = 0;
        GoldBlocked = 0;
        TurnWhenObtained = RelicStatsRegistry.TurnCount;
        CombatWhenObtained = RelicStatsRegistry.CombatCount;
        FrozenTurnCount = null;
        FrozenCombatCount = null;
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
}

// SealOfGold: spends gold for energy each turn
[HarmonyPatch]
public sealed class SealOfGoldStats : IRelicStats
{
    public string RelicId => RelicIdHelper.Slugify(nameof(SealOfGold));
    public int TurnWhenObtained { get; set; }
    public int CombatWhenObtained { get; set; }
    public int? FrozenTurnCount { get; set; }
    public int? FrozenCombatCount { get; set; }

    public int EnergyGenerated { get; set; }
    public int GoldSpent { get; set; }

    public string GetDescription(int totalTurns, int totalCombats)
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
            ["turnObtained"] = TurnWhenObtained,
            ["combatObtained"] = CombatWhenObtained,
        };
        if (FrozenTurnCount.HasValue) obj["frozenTurns"] = FrozenTurnCount.Value;
        if (FrozenCombatCount.HasValue) obj["frozenCombats"] = FrozenCombatCount.Value;
        return obj;
    }

    public void Load(JsonObject data)
    {
        EnergyGenerated = data["energy"]?.GetValue<int>() ?? 0;
        GoldSpent = data["goldSpent"]?.GetValue<int>() ?? 0;
        TurnWhenObtained = data["turnObtained"]?.GetValue<int>() ?? 0;
        CombatWhenObtained = data["combatObtained"]?.GetValue<int>() ?? 0;
        FrozenTurnCount = data["frozenTurns"]?.GetValue<int>();
        FrozenCombatCount = data["frozenCombats"]?.GetValue<int>();
    }

    public void Reset()
    {
        EnergyGenerated = 0;
        GoldSpent = 0;
        TurnWhenObtained = RelicStatsRegistry.TurnCount;
        CombatWhenObtained = RelicStatsRegistry.CombatCount;
        FrozenTurnCount = null;
        FrozenCombatCount = null;
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
}

// Sozu: +1 energy, blocks potion procurement
[HarmonyPatch]
public sealed class SozuStats : IRelicStats
{
    public string RelicId => RelicIdHelper.Slugify(nameof(Sozu));
    public int TurnWhenObtained { get; set; }
    public int CombatWhenObtained { get; set; }
    public int? FrozenTurnCount { get; set; }
    public int? FrozenCombatCount { get; set; }

    public int EnergyGenerated { get; set; }
    public int PotionsBlocked { get; set; }

    public string GetDescription(int totalTurns, int totalCombats)
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
            ["turnObtained"] = TurnWhenObtained,
            ["combatObtained"] = CombatWhenObtained,
        };
        if (FrozenTurnCount.HasValue) obj["frozenTurns"] = FrozenTurnCount.Value;
        if (FrozenCombatCount.HasValue) obj["frozenCombats"] = FrozenCombatCount.Value;
        return obj;
    }

    public void Load(JsonObject data)
    {
        EnergyGenerated = data["energy"]?.GetValue<int>() ?? 0;
        PotionsBlocked = data["potionsBlocked"]?.GetValue<int>() ?? 0;
        TurnWhenObtained = data["turnObtained"]?.GetValue<int>() ?? 0;
        CombatWhenObtained = data["combatObtained"]?.GetValue<int>() ?? 0;
        FrozenTurnCount = data["frozenTurns"]?.GetValue<int>();
        FrozenCombatCount = data["frozenCombats"]?.GetValue<int>();
    }

    public void Reset()
    {
        EnergyGenerated = 0;
        PotionsBlocked = 0;
        TurnWhenObtained = RelicStatsRegistry.TurnCount;
        CombatWhenObtained = RelicStatsRegistry.CombatCount;
        FrozenTurnCount = null;
        FrozenCombatCount = null;
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
}

// SpikedGauntlets: +1 energy, powers cost 1 more
[HarmonyPatch]
public sealed class SpikedGauntletsStats : IRelicStats
{
    public string RelicId => RelicIdHelper.Slugify(nameof(SpikedGauntlets));
    public int TurnWhenObtained { get; set; }
    public int CombatWhenObtained { get; set; }
    public int? FrozenTurnCount { get; set; }
    public int? FrozenCombatCount { get; set; }

    public int EnergyGenerated { get; set; }
    public int PowerCostIncrease { get; set; }

    public string GetDescription(int totalTurns, int totalCombats)
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
            ["turnObtained"] = TurnWhenObtained,
            ["combatObtained"] = CombatWhenObtained,
        };
        if (FrozenTurnCount.HasValue) obj["frozenTurns"] = FrozenTurnCount.Value;
        if (FrozenCombatCount.HasValue) obj["frozenCombats"] = FrozenCombatCount.Value;
        return obj;
    }

    public void Load(JsonObject data)
    {
        EnergyGenerated = data["energy"]?.GetValue<int>() ?? 0;
        PowerCostIncrease = data["powerCost"]?.GetValue<int>() ?? 0;
        TurnWhenObtained = data["turnObtained"]?.GetValue<int>() ?? 0;
        CombatWhenObtained = data["combatObtained"]?.GetValue<int>() ?? 0;
        FrozenTurnCount = data["frozenTurns"]?.GetValue<int>();
        FrozenCombatCount = data["frozenCombats"]?.GetValue<int>();
    }

    public void Reset()
    {
        EnergyGenerated = 0;
        PowerCostIncrease = 0;
        TurnWhenObtained = RelicStatsRegistry.TurnCount;
        CombatWhenObtained = RelicStatsRegistry.CombatCount;
        FrozenTurnCount = null;
        FrozenCombatCount = null;
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
}
