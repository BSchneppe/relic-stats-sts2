using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Models;

namespace RelicStats.Core;

// A changed unit cannot reuse old totals or divide its new partial history by old run rates.
public abstract class MeasuredCounterStats<TRelic> : SimpleCounterStats<TRelic> where TRelic : RelicModel
{
    protected abstract string PreviousMeasurement { get; }
    private int _previousAmount;
    private bool _changedDuringRun;

    public override string GetDescription(int effectiveTurns, int effectiveCombats)
    {
        if (!_changedDuringRun) return base.GetDescription(effectiveTurns, effectiveCombats);
        var text = FormatStat(Amount) + "\nMeasured since update.";
        if (_previousAmount > 0) text += $"\nEarlier {PreviousMeasurement}: {Fmt.Blue(_previousAmount)}.";
        return text;
    }

    public override JsonObject Save()
    {
        var data = base.Save();
        data["measurementVersion"] = 1;
        data["previousAmount"] = _previousAmount;
        data["changedDuringRun"] = _changedDuringRun;
        return data;
    }

    public override void Load(JsonObject data)
    {
        if (data["measurementVersion"]?.GetValue<int>() == 1)
        {
            base.Load(data);
            _previousAmount = data["previousAmount"]?.GetValue<int>() ?? 0;
            _changedDuringRun = data["changedDuringRun"]?.GetValue<bool>() ?? false;
        }
        else
        {
            _previousAmount = data["amount"]?.GetValue<int>() ?? 0;
            _changedDuringRun = true;
            Amount = 0;
        }
    }

    public override void Reset()
    {
        base.Reset();
        _previousAmount = 0;
        _changedDuringRun = false;
    }
}
