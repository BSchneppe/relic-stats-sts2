using System.Globalization;
using System.Text.Json.Nodes;

namespace RelicStats.Tests;

/// <summary>
/// Tests the core SimpleCounterStats logic (averages, serialization) without game dependencies.
/// We replicate the pure logic here since the actual class depends on game types (RelicModel, LocalContext).
/// </summary>
public class SimpleCounterStatsTests
{
    // Replicate the description formatting logic from SimpleCounterStats.
    // After the derive-from-game-state refactor, GetDescription takes effective denominators directly.
    private static string FormatDescription(string format, int amount, int effectiveTurns, int effectiveCombats)
    {
        if (effectiveTurns < 1) effectiveTurns = 1;
        if (effectiveCombats < 1) effectiveCombats = 1;

        var perTurn = ((float)amount / effectiveTurns).ToString("0.#", CultureInfo.InvariantCulture);
        var perCombat = ((float)amount / effectiveCombats).ToString("0.#", CultureInfo.InvariantCulture);

        return $"{string.Format(format, amount)} ({perTurn}/turn, {perCombat}/combat)";
    }

    [Fact]
    public void Description_BasicFormatting()
    {
        var result = FormatDescription("Blocked {0} damage.", 42, 10, 4);
        Assert.Equal("Blocked 42 damage. (4.2/turn, 10.5/combat)", result);
    }

    [Fact]
    public void Description_ZeroAmount()
    {
        var result = FormatDescription("Blocked {0} damage.", 0, 10, 4);
        Assert.Equal("Blocked 0 damage. (0/turn, 0/combat)", result);
    }

    [Fact]
    public void Description_EffectiveDenominators()
    {
        // Effective: 10 turns, 5 combats (caller already computed this from map history)
        var result = FormatDescription("Healed {0} HP.", 50, 10, 5);
        Assert.Equal("Healed 50 HP. (5/turn, 10/combat)", result);
    }

    [Fact]
    public void Description_ClampsToMinimumOne()
    {
        // 0 effective turns/combats — should clamp to 1 to avoid division by zero
        var result = FormatDescription("Dealt {0} damage.", 10, 0, 0);
        Assert.Equal("Dealt 10 damage. (10/turn, 10/combat)", result);
    }

    [Fact]
    public void Description_FractionalAverages()
    {
        var result = FormatDescription("Gained {0} gold.", 7, 3, 2);
        Assert.Contains("2.3/turn", result);
        Assert.Contains("3.5/combat", result);
    }

    // Serialization tests — new simplified format (amount only)
    private static JsonObject SerializeStats(int amount)
    {
        return new JsonObject
        {
            ["amount"] = amount,
        };
    }

    private static int DeserializeAmount(JsonObject data)
    {
        return data["amount"]?.GetValue<int>() ?? 0;
    }

    [Fact]
    public void Serialization_RoundTrip()
    {
        var json = SerializeStats(42);
        var amount = DeserializeAmount(json);
        Assert.Equal(42, amount);
    }

    [Fact]
    public void Serialization_MissingFieldsDefaultToZero()
    {
        var json = new JsonObject();
        var amount = DeserializeAmount(json);
        Assert.Equal(0, amount);
    }

    [Fact]
    public void Serialization_OldFormatFieldsIgnored()
    {
        // Old save format had extra fields — they should be harmlessly ignored
        var json = new JsonObject
        {
            ["amount"] = 99,
            ["turnObtained"] = 5,
            ["combatObtained"] = 2,
            ["frozenTurns"] = 10,
            ["frozenCombats"] = 5,
        };
        var amount = DeserializeAmount(json);
        Assert.Equal(99, amount);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(100, 50, 25)]
    [InlineData(int.MaxValue, 1000, 500)]
    [InlineData(1, 1, 1)]
    public void Description_VariousInputs_DoesNotThrow(int amount, int turns, int combats)
    {
        var result = FormatDescription("Value: {0}.", amount, turns, combats);
        Assert.Contains($"Value: {amount}.", result);
        Assert.Contains("/turn", result);
        Assert.Contains("/combat", result);
    }
}
