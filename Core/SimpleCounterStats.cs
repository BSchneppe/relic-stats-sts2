using System;
using System.Globalization;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Models;

namespace RelicStats.Core;

public abstract class SimpleCounterStats<TRelic> : IRelicStats where TRelic : RelicModel
{
    public string RelicId { get; } = RelicIdHelper.Slugify(typeof(TRelic).Name);
    public abstract string Format { get; }
    public int Amount { get; set; }

    /// <summary>
    /// Formats the stat line with the amount. Override to change number color (e.g., green for healing).
    /// </summary>
    protected virtual string FormatStat(int amount) => string.Format(Format, Fmt.Blue(amount));

    /// <summary>
    /// Override this to use green for healing numbers.
    /// </summary>
    protected string FormatStatGreen(int amount) => string.Format(Format, Fmt.Green(amount));
    public int TurnWhenObtained { get; set; }
    public int CombatWhenObtained { get; set; }
    public int? FrozenTurnCount { get; set; }
    public int? FrozenCombatCount { get; set; }

    public string GetDescription(int totalTurns, int totalCombats)
    {
        var effectiveTurns = (FrozenTurnCount ?? totalTurns) - TurnWhenObtained;
        var effectiveCombats = (FrozenCombatCount ?? totalCombats) - CombatWhenObtained;
        if (effectiveTurns < 1) effectiveTurns = 1;
        if (effectiveCombats < 1) effectiveCombats = 1;

        var perTurn = ((float)Amount / effectiveTurns).ToString("0.#", CultureInfo.InvariantCulture);
        var perCombat = ((float)Amount / effectiveCombats).ToString("0.#", CultureInfo.InvariantCulture);

        return $"{FormatStat(Amount)} ({Fmt.Blue(perTurn)}/turn, {Fmt.Blue(perCombat)}/combat)";
    }

    public JsonObject Save()
    {
        var obj = new JsonObject
        {
            ["amount"] = Amount,
            ["turnObtained"] = TurnWhenObtained,
            ["combatObtained"] = CombatWhenObtained,
        };
        if (FrozenTurnCount.HasValue) obj["frozenTurns"] = FrozenTurnCount.Value;
        if (FrozenCombatCount.HasValue) obj["frozenCombats"] = FrozenCombatCount.Value;
        return obj;
    }

    public void Load(JsonObject data)
    {
        Amount = data["amount"]?.GetValue<int>() ?? 0;
        TurnWhenObtained = data["turnObtained"]?.GetValue<int>() ?? 0;
        CombatWhenObtained = data["combatObtained"]?.GetValue<int>() ?? 0;
        FrozenTurnCount = data["frozenTurns"]?.GetValue<int>();
        FrozenCombatCount = data["frozenCombats"]?.GetValue<int>();
    }

    public void Reset()
    {
        Amount = 0;
        TurnWhenObtained = RelicStatsRegistry.TurnCount;
        CombatWhenObtained = RelicStatsRegistry.CombatCount;
        FrozenTurnCount = null;
        FrozenCombatCount = null;
    }

    public static bool Track(TRelic instance, Action<SimpleCounterStats<TRelic>> update)
    {
        if (instance.IsMelted) return false;
        if (!LocalContext.IsMine(instance)) return false;
        if (RelicStatsRegistry.Get(RelicIdHelper.Slugify(typeof(TRelic).Name)) is not SimpleCounterStats<TRelic> stats) return false;
        update(stats);
        return true;
    }
}
