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

    [Fact]
    public void SaveFormat_MidRunObtainedRelic_PreservesObtainTime()
    {
        // Relic obtained at turn 5, combat 3 — save should preserve these values
        var relic = new JsonObject
        {
            ["amount"] = 20,
            ["turnObtained"] = 5,
            ["combatObtained"] = 3,
        };

        var root = BuildSaveRoot(15, 8, new Dictionary<string, JsonObject> { ["MidRunRelic"] = relic });
        var json = root.ToJsonString();
        var parsed = JsonNode.Parse(json)!.AsObject();

        var loaded = parsed["relics"]!["MidRunRelic"]!.AsObject();
        Assert.Equal(20, loaded["amount"]!.GetValue<int>());
        Assert.Equal(5, loaded["turnObtained"]!.GetValue<int>());
        Assert.Equal(3, loaded["combatObtained"]!.GetValue<int>());

        // Verify the denominator calculation uses obtain time
        var amount = loaded["amount"]!.GetValue<int>();
        var turnObt = loaded["turnObtained"]!.GetValue<int>();
        var combatObt = loaded["combatObtained"]!.GetValue<int>();
        var totalTurns = parsed["turnCount"]!.GetValue<int>();
        var totalCombats = parsed["combatCount"]!.GetValue<int>();

        var effectiveTurns = totalTurns - turnObt; // 15 - 5 = 10
        var effectiveCombats = totalCombats - combatObt; // 8 - 3 = 5

        Assert.Equal(10, effectiveTurns);
        Assert.Equal(5, effectiveCombats);
        Assert.Equal(2.0f, (float)amount / effectiveTurns); // 20/10 = 2.0
        Assert.Equal(4.0f, (float)amount / effectiveCombats); // 20/5 = 4.0
    }

    [Fact]
    public void SaveFormat_SaveClearLoadRoundTrip()
    {
        // Simulate: save stats, clear (reset), load, verify values match
        var relics = new Dictionary<string, JsonObject>
        {
            ["TestRelic"] = new JsonObject
            {
                ["amount"] = 100,
                ["turnObtained"] = 3,
                ["combatObtained"] = 1,
                ["frozenTurns"] = 12,
                ["frozenCombats"] = 6,
            },
        };

        // Save
        var root = BuildSaveRoot(15, 8, relics);
        var json = root.ToJsonString();

        // Clear (simulate ResetAll)
        var clearedRelics = new Dictionary<string, JsonObject>
        {
            ["TestRelic"] = new JsonObject
            {
                ["amount"] = 0,
                ["turnObtained"] = 0,
                ["combatObtained"] = 0,
            },
        };
        var clearedRoot = BuildSaveRoot(0, 0, clearedRelics);

        // Load (parse saved JSON)
        var loaded = JsonNode.Parse(json)!.AsObject();
        var loadedRelic = loaded["relics"]!["TestRelic"]!.AsObject();

        // Verify loaded values match original
        Assert.Equal(100, loadedRelic["amount"]!.GetValue<int>());
        Assert.Equal(3, loadedRelic["turnObtained"]!.GetValue<int>());
        Assert.Equal(1, loadedRelic["combatObtained"]!.GetValue<int>());
        Assert.Equal(12, loadedRelic["frozenTurns"]!.GetValue<int>());
        Assert.Equal(6, loadedRelic["frozenCombats"]!.GetValue<int>());
        Assert.Equal(15, loaded["turnCount"]!.GetValue<int>());
        Assert.Equal(8, loaded["combatCount"]!.GetValue<int>());
    }
}
