using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Models;

namespace RelicStats.Core;

// New completed-effect measurements never reinterpret the historical activation/request total.
public abstract class MiscRecordedCounterStats<TRelic> : SimpleCounterStats<TRelic> where TRelic : RelicModel
{
    public int RecordedAmount { get; set; }
    protected abstract string RecordedKey { get; }
    protected abstract string RecordedFormat { get; }

    public override string GetDescription(int effectiveTurns, int effectiveCombats) =>
        string.Format(RecordedFormat, Fmt.Blue(RecordedAmount)) + "\n" + base.GetDescription(effectiveTurns, effectiveCombats);

    public override JsonObject Save()
    {
        var data = base.Save();
        data[RecordedKey] = RecordedAmount;
        return data;
    }

    public override void Load(JsonObject data)
    {
        base.Load(data);
        RecordedAmount = data[RecordedKey]?.GetValue<int>() ?? 0;
    }

    public override void Reset()
    {
        base.Reset();
        RecordedAmount = 0;
    }
}
