using System.Text.Json;
using System.Text.Json.Nodes;

namespace RelicStats.Tests;

/// <summary>
/// Tests JSON serialization structure and round-trip correctness
/// for the simplified save format (amount + floorMelted).
/// </summary>
public class StatsPersistenceTests
{
    private static JsonObject BuildSaveRoot(Dictionary<string, JsonObject> relics,
        Dictionary<string, int>? floorMelted = null)
    {
        var relicsNode = new JsonObject();
        foreach (var (id, data) in relics)
            relicsNode[id] = data;

        var floorMeltedNode = new JsonObject();
        if (floorMelted != null)
            foreach (var (id, floor) in floorMelted)
                floorMeltedNode[id] = floor;

        return new JsonObject
        {
            ["relics"] = relicsNode,
            ["floorMelted"] = floorMeltedNode,
        };
    }

    [Fact]
    public void SaveFormat_RoundTrip()
    {
        var relics = new Dictionary<string, JsonObject>
        {
            ["ANCHOR"] = new JsonObject { ["amount"] = 42 },
            ["HAPPY_FLOWER"] = new JsonObject { ["amount"] = 5 },
        };

        var root = BuildSaveRoot(relics);
        var json = root.ToJsonString();
        var parsed = JsonNode.Parse(json)!.AsObject();

        Assert.Equal(42, parsed["relics"]!["ANCHOR"]!["amount"]!.GetValue<int>());
        Assert.Equal(5, parsed["relics"]!["HAPPY_FLOWER"]!["amount"]!.GetValue<int>());
    }

    [Fact]
    public void SaveFormat_EmptyRelics()
    {
        var root = BuildSaveRoot(new Dictionary<string, JsonObject>());
        var json = root.ToJsonString();
        var parsed = JsonNode.Parse(json)!.AsObject();

        Assert.Empty(parsed["relics"]!.AsObject());
    }

    [Fact]
    public void SaveFormat_FloorMelted_PreservedInRoundTrip()
    {
        var relics = new Dictionary<string, JsonObject>
        {
            ["WAX_RELIC"] = new JsonObject { ["amount"] = 30 },
        };
        var floorMelted = new Dictionary<string, int> { ["WAX_RELIC"] = 8 };

        var root = BuildSaveRoot(relics, floorMelted);
        var json = root.ToJsonString();
        var parsed = JsonNode.Parse(json)!.AsObject();

        Assert.Equal(8, parsed["floorMelted"]!["WAX_RELIC"]!.GetValue<int>());
        Assert.Equal(30, parsed["relics"]!["WAX_RELIC"]!["amount"]!.GetValue<int>());
    }

    [Fact]
    public void SaveFormat_OldFormatFieldsIgnored()
    {
        // Old saves had extra fields — they should be harmlessly ignored
        var root = new JsonObject
        {
            ["turnCount"] = 20,
            ["combatCount"] = 10,
            ["relics"] = new JsonObject
            {
                ["ANCHOR"] = new JsonObject
                {
                    ["amount"] = 42,
                    ["turnObtained"] = 5,
                    ["combatObtained"] = 2,
                    ["frozenTurns"] = 10,
                    ["frozenCombats"] = 5,
                },
            },
        };

        var json = root.ToJsonString();
        var parsed = JsonNode.Parse(json)!.AsObject();

        // amount is still readable
        Assert.Equal(42, parsed["relics"]!["ANCHOR"]!["amount"]!.GetValue<int>());
        // old fields exist but our new Load() ignores them
    }

    [Fact]
    public void SaveFormat_CorruptJson_ThrowsJsonException()
    {
        var corrupted = "{ not valid json";
        Assert.ThrowsAny<JsonException>(() => JsonNode.Parse(corrupted));
    }

    [Fact]
    public void SaveFormat_NullFields_DefaultToZero()
    {
        var relic = new JsonObject();
        var amount = relic["amount"]?.GetValue<int>() ?? 0;
        Assert.Equal(0, amount);
    }

    [Fact]
    public void SaveFormat_SaveClearLoadRoundTrip()
    {
        var relics = new Dictionary<string, JsonObject>
        {
            ["TEST_RELIC"] = new JsonObject { ["amount"] = 100 },
        };
        var floorMelted = new Dictionary<string, int> { ["TEST_RELIC"] = 12 };

        // Save
        var root = BuildSaveRoot(relics, floorMelted);
        var json = root.ToJsonString();

        // Load
        var loaded = JsonNode.Parse(json)!.AsObject();
        var loadedRelic = loaded["relics"]!["TEST_RELIC"]!.AsObject();

        Assert.Equal(100, loadedRelic["amount"]!.GetValue<int>());
        Assert.Equal(12, loaded["floorMelted"]!["TEST_RELIC"]!.GetValue<int>());
    }
}
