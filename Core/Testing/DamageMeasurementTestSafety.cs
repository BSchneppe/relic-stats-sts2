#if DEBUG
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;

namespace RelicStats.Core.Testing;

internal static class DamageMeasurementTestSafety
{
    // Buffer would turn these measurement tests into fully prevented hits.
    public static void ProtectEnemies()
    {
        var enemies = TestHelpers.Player?.Creature.CombatState?.HittableEnemies;
        if (enemies == null) return;
        foreach (var enemy in enemies)
        {
            enemy.SetMaxHpInternal(999999);
            enemy.SetCurrentHpInternal(999999);
        }
    }

    public static void ProtectPlayer()
    {
        var creature = TestHelpers.Player?.Creature;
        if (creature == null) return;
        TaskHelper.RunSafely(PowerCmd.Apply<BufferPower>(new ThrowingPlayerChoiceContext(),
            creature, 999999999m, creature, null));
    }
}
#endif
