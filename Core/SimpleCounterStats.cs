using System;
using System.Globalization;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Models;
#if DEBUG
using RelicStats.Core.Testing;
#endif

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

    public string GetDescription(int effectiveTurns, int effectiveCombats)
    {
        if (effectiveTurns < 1) effectiveTurns = 1;
        if (effectiveCombats < 1) effectiveCombats = 1;

        var perTurn = ((float)Amount / effectiveTurns).ToString("0.###", CultureInfo.InvariantCulture);
        var perCombat = ((float)Amount / effectiveCombats).ToString("0.###", CultureInfo.InvariantCulture);

        return $"{FormatStat(Amount)}\nPer turn: {Fmt.Blue(perTurn)}\nPer combat: {Fmt.Blue(perCombat)}";
    }

    public JsonObject Save()
    {
        return new JsonObject
        {
            ["amount"] = Amount,
        };
    }

    public void Load(JsonObject data)
    {
        Amount = data["amount"]?.GetValue<int>() ?? 0;
    }

    public void Reset()
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
