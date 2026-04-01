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

    public int TurnWhenObtained { get; set; }
    public int CombatWhenObtained { get; set; }
    public int? FrozenTurnCount { get; set; }
    public int? FrozenCombatCount { get; set; }

    public string GetDescription(int totalTurns, int totalCombats) => "Validation-only.";
    public JsonObject Save() => new();
    public void Load(JsonObject data) { }
    public void Reset() { }

    public abstract void RegisterTest(TestRunner runner);
}
#endif
