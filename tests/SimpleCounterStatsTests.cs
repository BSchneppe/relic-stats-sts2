using System.Globalization;
using System.Text.Json.Nodes;

namespace RelicStats.Tests;

/// <summary>
/// Tests the core SimpleCounterStats logic (averages, serialization) without game dependencies.
/// We replicate the pure logic here since the actual class depends on game types (RelicModel, LocalContext).
/// </summary>
public class SimpleCounterStatsTests
{
    // Mirrors SimpleCounterStats.StatCadence: which rate lines follow the total.
    private enum Cadence { Turn, Combat, Total }

    // Replicate the description formatting logic from SimpleCounterStats.GetDescription.
    // GetDescription takes effective denominators directly; the cadence decides which rate lines
    // are worth showing (a fixed once-per-combat amount or an out-of-combat relic gets none).
    private static string FormatDescription(string format, int amount, int effectiveTurns, int effectiveCombats,
        Cadence cadence = Cadence.Turn)
    {
        if (effectiveTurns < 1) effectiveTurns = 1;
        if (effectiveCombats < 1) effectiveCombats = 1;

        var text = string.Format(format, amount);
        if (cadence == Cadence.Turn)
            text += "\nPer turn: " + ((float)amount / effectiveTurns).ToString("0.###", CultureInfo.InvariantCulture);
        if (cadence != Cadence.Total)
            text += "\nPer combat: " + ((float)amount / effectiveCombats).ToString("0.###", CultureInfo.InvariantCulture);
        return text;
    }

    [Fact]
    public void Description_BasicFormatting()
    {
        var result = FormatDescription("Blocked {0} damage.", 42, 10, 4);
        Assert.Equal("Blocked 42 damage.\nPer turn: 4.2\nPer combat: 10.5", result);
    }

    [Fact]
    public void Description_ZeroAmount()
    {
        var result = FormatDescription("Blocked {0} damage.", 0, 10, 4);
        Assert.Equal("Blocked 0 damage.\nPer turn: 0\nPer combat: 0", result);
    }

    [Fact]
    public void Description_EffectiveDenominators()
    {
        // Effective: 10 turns, 5 combats (caller already computed this from map history)
        var result = FormatDescription("Healed {0} HP.", 50, 10, 5);
        Assert.Equal("Healed 50 HP.\nPer turn: 5\nPer combat: 10", result);
    }

    [Fact]
    public void Description_ClampsToMinimumOne()
    {
        // 0 effective turns/combats — should clamp to 1 to avoid division by zero
        var result = FormatDescription("Dealt {0} damage.", 10, 0, 0);
        Assert.Equal("Dealt 10 damage.\nPer turn: 10\nPer combat: 10", result);
    }

    [Fact]
    public void Description_FractionalAverages()
    {
        var result = FormatDescription("Gained {0} gold.", 7, 3, 2);
        Assert.Contains("Per turn: 2.333", result);
        Assert.Contains("Per combat: 3.5", result);
    }

    [Fact]
    public void Description_CombatCadence_OmitsPerTurn()
    {
        // Festive Popper: once per combat, damage × enemy count varies → only the per-combat average.
        var result = FormatDescription("Dealt {0} damage.", 60, 30, 4, Cadence.Combat);
        Assert.Equal("Dealt 60 damage.\nPer combat: 15", result);
        Assert.DoesNotContain("Per turn", result);
    }

    [Fact]
    public void Description_TotalCadence_OmitsBothRates()
    {
        // Lantern: 1 energy on turn 1 of every combat → a rate line would always read 1.
        var result = FormatDescription("Generated {0} energy.", 12, 40, 12, Cadence.Total);
        Assert.Equal("Generated 12 energy.", result);
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
        Assert.Contains("Per turn: ", result);
        Assert.Contains("Per combat: ", result);
    }
}
