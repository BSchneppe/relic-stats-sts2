using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace RelicStats.Core;

public static class RelicIdHelper
{
    private static readonly Regex CamelCaseRegex = new(@"([a-z])([A-Z])", RegexOptions.Compiled);

    public static string Slugify(string typeName) =>
        CamelCaseRegex.Replace(typeName, "$1_$2").ToUpperInvariant();
}

public interface IRelicStats
{
    string RelicId { get; }
    string GetDescription(int totalTurns, int totalCombats);
    JsonObject Save();
    void Load(JsonObject data);
    void Reset();
    int TurnWhenObtained { get; set; }
    int CombatWhenObtained { get; set; }
    int? FrozenTurnCount { get; set; }
    int? FrozenCombatCount { get; set; }
}
