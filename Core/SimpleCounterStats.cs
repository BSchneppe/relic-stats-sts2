using System;
using System.Globalization;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Models;
#if DEBUG
using RelicStats.Core.Testing;
#endif

namespace RelicStats.Core;

/// <summary>
/// Which rate lines a stat's tooltip shows under its total. A rate is only worth a line when it can
/// vary: a relic that fires at most once per combat for a fixed amount (Lantern, Anchor) says nothing
/// new with "Per combat: 1", and an out-of-combat relic (Maw Bank, the eggs) says nothing with either.
/// </summary>
public enum StatCadence
{
    /// <summary>Fires per card, per turn, per hit… — show per-turn and per-combat averages.</summary>
    Turn,
    /// <summary>At most once per combat, but the amount varies (damage × enemies, actual HP healed) — show a per-combat average only.</summary>
    Combat,
    /// <summary>Fixed once-per-combat amount, a trigger count that caps at 1 per combat, or an out-of-combat relic — show the total only.</summary>
    Total,
}

public abstract class SimpleCounterStats<TRelic> : IRelicStats where TRelic : RelicModel
{
    public string RelicId { get; } = RelicIdHelper.Slugify(typeof(TRelic).Name);
    public abstract string Format { get; }
    public virtual StatCadence Cadence => StatCadence.Turn;
    public int Amount { get; set; }

    /// <summary>
    /// Formats the stat line with the amount. Override to change number color (e.g., green for healing).
    /// </summary>
    protected virtual string FormatStat(int amount) => string.Format(Format, Fmt.Blue(amount));

    /// <summary>
    /// Override this to use green for healing numbers.
    /// </summary>
    protected string FormatStatGreen(int amount) => string.Format(Format, Fmt.Green(amount));

    public virtual string GetDescription(int effectiveTurns, int effectiveCombats)
    {
        if (effectiveTurns < 1) effectiveTurns = 1;
        if (effectiveCombats < 1) effectiveCombats = 1;

        var text = FormatStat(Amount);
        if (Cadence == StatCadence.Turn)
        {
            var perTurn = ((float)Amount / effectiveTurns).ToString("0.###", CultureInfo.InvariantCulture);
            text += $"\nPer turn: {Fmt.Blue(perTurn)}";
        }
        if (Cadence != StatCadence.Total)
        {
            var perCombat = ((float)Amount / effectiveCombats).ToString("0.###", CultureInfo.InvariantCulture);
            text += $"\nPer combat: {Fmt.Blue(perCombat)}";
        }
        return text;
    }

    public virtual JsonObject Save()
    {
        return new JsonObject
        {
            ["amount"] = Amount,
        };
    }

    public virtual void Load(JsonObject data)
    {
        Amount = data["amount"]?.GetValue<int>() ?? 0;
    }

    public virtual void Reset()
    {
        Amount = 0;
    }

#if DEBUG
    public abstract void RegisterTest(TestRunner runner);
#endif

    public static bool Track(TRelic instance, Action<SimpleCounterStats<TRelic>> update)
    {
#if DEBUG
        var relicId = RelicIdHelper.Slugify(typeof(TRelic).Name);
        if (TestManager.IsRunning)
        {
            if (instance.IsMelted) { MainFile.Logger.Info($"[Track] {relicId}: skipped (melted)"); return false; }
            if (!LocalContext.IsMine(instance)) { MainFile.Logger.Info($"[Track] {relicId}: skipped (not mine, owner={instance.Owner?.NetId}, local={LocalContext.NetId})"); return false; }
            if (RelicStatsRegistry.Get(relicId) is not SimpleCounterStats<TRelic> s) { MainFile.Logger.Info($"[Track] {relicId}: skipped (not in registry)"); return false; }
            update(s);
            MainFile.Logger.Info($"[Track] {relicId}: tracked, Amount={s.Amount}");
            return true;
        }
#endif
        if (instance.IsMelted) return false;
        if (!LocalContext.IsMine(instance)) return false;
        if (RelicStatsRegistry.Get(RelicIdHelper.Slugify(typeof(TRelic).Name)) is not SimpleCounterStats<TRelic> stats) return false;
        update(stats);
        return true;
    }
}
