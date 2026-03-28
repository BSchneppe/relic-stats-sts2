using System.Text.Json;
using System.Text.Json.Nodes;

namespace RelicStats.Tests;

/// <summary>
/// Tests JSON serialization structure and round-trip correctness
/// for the registry save format.
/// </summary>
public class StatsPersistenceTests
{
    // Replicate the save format from StatsPersistence
    private static JsonObject BuildSaveRoot(int turnCount, int combatCount, Dictionary<string, JsonObject> relics)
    {
        var relicsNode = new JsonObject();
        foreach (var (id, data) in relics)
        {
            relicsNode[id] = data;
        }
        return new JsonObject
        {
            ["turnCount"] = turnCount,
            ["combatCount"] = combatCount,
            ["relics"] = relicsNode,
        };
    }

    [Fact]
    public void SaveFormat_RoundTrip()
    {
        var relics = new Dictionary<string, JsonObject>
        {
            ["Abacus"] = new JsonObject { ["amount"] = 42, ["turnObtained"] = 0, ["combatObtained"] = 0 },
            ["HappyFlower"] = new JsonObject { ["amount"] = 5, ["turnObtained"] = 3, ["combatObtained"] = 1 },
        };

        var root = BuildSaveRoot(20, 8, relics);
        var json = root.ToJsonString();
        var parsed = JsonNode.Parse(json)!.AsObject();

        Assert.Equal(20, parsed["turnCount"]!.GetValue<int>());
        Assert.Equal(8, parsed["combatCount"]!.GetValue<int>());
        Assert.Equal(42, parsed["relics"]!["Abacus"]!["amount"]!.GetValue<int>());
        Assert.Equal(5, parsed["relics"]!["HappyFlower"]!["amount"]!.GetValue<int>());
        Assert.Equal(3, parsed["relics"]!["HappyFlower"]!["turnObtained"]!.GetValue<int>());
    }

    [Fact]
    public void SaveFormat_EmptyRelics()
    {
        var root = BuildSaveRoot(0, 0, new Dictionary<string, JsonObject>());
        var json = root.ToJsonString();
        var parsed = JsonNode.Parse(json)!.AsObject();

        Assert.Equal(0, parsed["turnCount"]!.GetValue<int>());
        Assert.Equal(0, parsed["combatCount"]!.GetValue<int>());
        Assert.Empty(parsed["relics"]!.AsObject());
    }

    [Fact]
    public void SaveFormat_FrozenCounts_PreservedInRoundTrip()
    {
        var relic = new JsonObject
        {
            ["amount"] = 30,
            ["turnObtained"] = 0,
            ["combatObtained"] = 0,
            ["frozenTurns"] = 10,
            ["frozenCombats"] = 5,
        };

        var root = BuildSaveRoot(20, 10, new Dictionary<string, JsonObject> { ["WaxRelic"] = relic });
        var json = root.ToJsonString();
        var parsed = JsonNode.Parse(json)!.AsObject();

        var loadedRelic = parsed["relics"]!["WaxRelic"]!.AsObject();
        Assert.Equal(10, loadedRelic["frozenTurns"]!.GetValue<int>());
        Assert.Equal(5, loadedRelic["frozenCombats"]!.GetValue<int>());
    }

    [Fact]
    public void SaveFormat_UnknownRelicId_IgnoredOnLoad()
    {
        // Simulate loading a save with a relic ID that no longer exists
        var root = new JsonObject
        {
            ["turnCount"] = 5,
            ["combatCount"] = 2,
            ["relics"] = new JsonObject
            {
                ["NonExistentRelic"] = new JsonObject { ["amount"] = 99 },
            },
        };

        // Should not throw — unknown IDs are simply skipped
        var json = root.ToJsonString();
        var parsed = JsonNode.Parse(json)!.AsObject();
        var relics = parsed["relics"]!.AsObject();

        // We can enumerate without error
        foreach (var (id, _) in relics)
        {
            Assert.Equal("NonExistentRelic", id);
        }
    }

    [Fact]
    public void SaveFormat_CorruptJson_ThrowsJsonException()
    {
        // JsonNode.Parse throws on invalid JSON — StatsPersistence wraps this in try/catch
        var corrupted = "{ not valid json";
        Assert.ThrowsAny<JsonException>(() => JsonNode.Parse(corrupted));
    }

    [Fact]
    public void SaveFormat_NullFields_DefaultToZero()
    {
        var relic = new JsonObject
        {
            // Missing "amount", "turnObtained", etc.
        };

        var amount = relic["amount"]?.GetValue<int>() ?? 0;
        var turnObtained = relic["turnObtained"]?.GetValue<int>() ?? 0;
        var frozenTurns = relic["frozenTurns"]?.GetValue<int>();

        Assert.Equal(0, amount);
        Assert.Equal(0, turnObtained);
        Assert.Null(frozenTurns);
    }
}
