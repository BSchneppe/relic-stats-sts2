using MegaCrit.Sts2.Core.Runs;

namespace RelicStats.Core;

/// <summary>
/// Shared BBCode formatting helpers matching STS2's tooltip color conventions.
/// [blue] = generic numbers, [green] = healing/HP, [gold] = game keywords.
/// </summary>
public static class Fmt
{
    // Number formatting
    public static string Blue(int n) => $"[blue]{n}[/blue]";
    public static string Blue(string s) => $"[blue]{s}[/blue]";
    public static string Green(int n) => $"[green]{n}[/green]";
    public static string Green(string s) => $"[green]{s}[/green]";

    // Keyword formatting
    public static string Gold(string s) => $"[gold]{s}[/gold]";

    // Common game keywords (pre-formatted)
    public static readonly string Block = Gold("Block");
    public static readonly string Damage = Gold("Damage");
    public static readonly string HP = "HP";
    public static readonly string GoldKw = Gold("Gold");
    public static readonly string Vigor = Gold("Vigor");
    public static readonly string Strength = Gold("Strength");
    public static readonly string Dexterity = Gold("Dexterity");
    public static readonly string Exhaust = Gold("Exhaust");
    public static readonly string Poison = Gold("Poison");
    public static readonly string Plating = Gold("Plating");
    public static readonly string Stars = Gold("Stars");
    public static readonly string Lightning = Gold("Lightning");
    public static readonly string Vulnerable = Gold("Vulnerable");
    public static readonly string Weak = Gold("Weak");

    /// <summary>
    /// Returns the energy icon BBCode for the local player's character class.
    /// Falls back to "[gold]Energy[/gold]" if no run is active.
    /// </summary>
    /// <summary>
    /// Returns "42 [energy icon]" — the number + one energy icon.
    /// Falls back to "42 Energy" if no run is active.
    /// </summary>
    public static string EnergyIcon(int amount)
    {
        var prefix = RunManager.Instance?.GetLocalCharacterEnergyIconPrefix();
        if (string.IsNullOrEmpty(prefix))
            return $"{Blue(amount)} {Gold("Energy")}";

        var path = $"res://images/packed/sprite_fonts/{prefix}_energy_icon.png";
        return $"{Blue(amount)} [img]{path}[/img]";
    }
}
