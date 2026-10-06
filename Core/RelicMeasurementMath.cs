using System;

namespace RelicStats.Core;

internal static class RelicMeasurementMath
{
    internal static int ExtraHealing(int actualHeal, int baseHeal, int missingHp) =>
        Math.Max(0, Math.Min(actualHeal, missingHp) - Math.Min(baseHeal, missingHp));

    internal static bool TryRollDiscount(bool costsX, int upgradedBase, int roll, out int discount)
    {
        discount = 0;
        if (costsX || upgradedBase < 0 || roll < 0 || roll > 3) return false;
        discount = upgradedBase - roll;
        return true;
    }
}
