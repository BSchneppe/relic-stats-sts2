using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;

namespace RelicStats.Core;

internal static class DirectDamageScope
{
    public static IDisposable Begin(Player owner, Action<IEnumerable<DamageResult>> record) =>
        EffectScope<IEnumerable<DamageResult>>.Begin(owner, record);

    public static int DamageDealt(IEnumerable<DamageResult> results) =>
        results.Sum(result => result.BlockedDamage + result.UnblockedDamage);

    public static int HpLost(IEnumerable<DamageResult> results, Creature recipient) =>
        results.Where(result => result.Receiver == recipient).Sum(result => result.UnblockedDamage);
}
