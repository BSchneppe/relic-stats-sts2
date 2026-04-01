#if DEBUG
using System.Text.Json.Nodes;

namespace RelicStats.Core.Testing;

/// <summary>
/// Base class for debug-only validation tests. Not real relics — these exist solely
/// to verify the mod doesn't break game systems (events, dialogues, etc.).
/// Registered in RelicStatsRegistry so they run with `relicstats test`.
/// </summary>
public abstract class ValidationTest : IRelicStats
{
    public abstract string RelicId { get; }

    public string GetDescription(int effectiveTurns, int effectiveCombats) => "Validation-only.";
    public JsonObject Save() => new();
    public void Load(JsonObject data) { }
    public void Reset() { }

    public abstract void RegisterTest(TestRunner runner);
}
#endif
