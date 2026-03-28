using System.Globalization;
using System.Text.Json.Nodes;

namespace RelicStats.Tests;

/// <summary>
/// Tests the core SimpleCounterStats logic (averages, serialization) without game dependencies.
/// We replicate the pure logic here since the actual class depends on game types (RelicModel, LocalContext).
/// </summary>
public class SimpleCounterStatsTests
{
    // Replicate the description formatting logic from SimpleCounterStats
    private static string FormatDescription(string format, int amount, int turnObtained, int combatObtained,
        int totalTurns, int totalCombats, int? frozenTurns = null, int? frozenCombats = null)
    {
        var effectiveTurns = (frozenTurns ?? totalTurns) - turnObtained;
        var effectiveCombats = (frozenCombats ?? totalCombats) - combatObtained;
        if (effectiveTurns < 1) effectiveTurns = 1;
        if (effectiveCombats < 1) effectiveCombats = 1;

        var perTurn = ((float)amount / effectiveTurns).ToString("0.#", CultureInfo.InvariantCulture);
        var perCombat = ((float)amount / effectiveCombats).ToString("0.#", CultureInfo.InvariantCulture);

        return $"{string.Format(format, amount)} ({perTurn}/turn, {perCombat}/combat)";
    }

    [Fact]
    public void Description_BasicFormatting()
    {
        var result = FormatDescription("Blocked {0} damage.", 42, 0, 0, 10, 4);
        Assert.Equal("Blocked 42 damage. (4.2/turn, 10.5/combat)", result);
    }

    [Fact]
    public void Description_ZeroAmount()
    {
        var result = FormatDescription("Blocked {0} damage.", 0, 0, 0, 10, 4);
        Assert.Equal("Blocked 0 damage. (0/turn, 0/combat)", result);
    }

    [Fact]
    public void Description_RelicObtainedMidRun()
    {
        // Obtained on turn 5, combat 2. Now at turn 15, combat 7.
        // Effective: 10 turns, 5 combats
        var result = FormatDescription("Healed {0} HP.", 50, 5, 2, 15, 7);
        Assert.Equal("Healed 50 HP. (5/turn, 10/combat)", result);
    }

    [Fact]
    public void Description_ClampsToMinimumOne()
    {
        // Turn 0, combat 0 — should clamp to 1 to avoid division by zero
        var result = FormatDescription("Dealt {0} damage.", 10, 0, 0, 0, 0);
        Assert.Equal("Dealt 10 damage. (10/turn, 10/combat)", result);
    }

    [Fact]
    public void Description_FrozenCountsForMeltedRelics()
    {
        // Relic obtained turn 0/combat 0, melted at turn 10/combat 5
        // Current global is turn 20/combat 10, but frozen counts should be used
        var result = FormatDescription("Blocked {0} damage.", 30, 0, 0, 20, 10,
            frozenTurns: 10, frozenCombats: 5);
        Assert.Equal("Blocked 30 damage. (3/turn, 6/combat)", result);
    }

    [Fact]
    public void Description_FractionalAverages()
    {
        var result = FormatDescription("Gained {0} gold.", 7, 0, 0, 3, 2);
        Assert.Contains("2.3/turn", result);
        Assert.Contains("3.5/combat", result);
    }

    // Serialization round-trip tests using the same JSON structure as SimpleCounterStats
    private static JsonObject SerializeStats(int amount, int turnObtained, int combatObtained,
        int? frozenTurns = null, int? frozenCombats = null)
    {
        var obj = new JsonObject
        {
            ["amount"] = amount,
            ["turnObtained"] = turnObtained,
            ["combatObtained"] = combatObtained,
        };
        if (frozenTurns.HasValue) obj["frozenTurns"] = frozenTurns.Value;
        if (frozenCombats.HasValue) obj["frozenCombats"] = frozenCombats.Value;
        return obj;
    }

    private static (int amount, int turnObtained, int combatObtained, int? frozenTurns, int? frozenCombats)
        DeserializeStats(JsonObject data)
    {
        return (
            data["amount"]?.GetValue<int>() ?? 0,
            data["turnObtained"]?.GetValue<int>() ?? 0,
            data["combatObtained"]?.GetValue<int>() ?? 0,
            data["frozenTurns"]?.GetValue<int>(),
            data["frozenCombats"]?.GetValue<int>()
        );
    }

    [Fact]
    public void Serialization_RoundTrip()
    {
        var json = SerializeStats(42, 5, 2);
        var (amount, turn, combat, frozen, frozenC) = DeserializeStats(json);

        Assert.Equal(42, amount);
        Assert.Equal(5, turn);
        Assert.Equal(2, combat);
        Assert.Null(frozen);
        Assert.Null(frozenC);
    }

    [Fact]
    public void Serialization_RoundTripWithFrozenCounts()
    {
        var json = SerializeStats(30, 0, 0, frozenTurns: 10, frozenCombats: 5);
        var (amount, turn, combat, frozen, frozenC) = DeserializeStats(json);

        Assert.Equal(30, amount);
        Assert.Equal(0, turn);
        Assert.Equal(0, combat);
        Assert.Equal(10, frozen);
        Assert.Equal(5, frozenC);
    }

    [Fact]
    public void Serialization_MissingFieldsDefaultToZero()
    {
        var json = new JsonObject();
        var (amount, turn, combat, frozen, frozenC) = DeserializeStats(json);

        Assert.Equal(0, amount);
        Assert.Equal(0, turn);
        Assert.Equal(0, combat);
        Assert.Null(frozen);
        Assert.Null(frozenC);
    }

    [Theory]
    [InlineData(0, 0, 0, 1, 1)]
    [InlineData(100, 0, 0, 50, 25)]
    [InlineData(int.MaxValue, 0, 0, 1000, 500)]
    [InlineData(1, 999, 499, 1000, 500)]
    public void Description_VariousInputs_DoesNotThrow(int amount, int turnObt, int combatObt, int turns, int combats)
    {
        var result = FormatDescription("Value: {0}.", amount, turnObt, combatObt, turns, combats);
        Assert.Contains($"Value: {amount}.", result);
        Assert.Contains("/turn", result);
        Assert.Contains("/combat", result);
    }
}
