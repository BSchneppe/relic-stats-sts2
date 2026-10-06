using System;
using System.Collections.Generic;
using System.Threading;

namespace RelicStats.Core;

internal sealed class RestHealScope : IDisposable
{
    private static readonly AsyncLocal<RestHealScope?> Current = new();
    private readonly RestHealScope? _previous;
    private readonly Dictionary<object, Action<int>> _candidates = new(ReferenceEqualityComparer.Instance);
    private bool _restored;

    private RestHealScope() => _previous = Current.Value;
    internal static RestHealScope Begin()
    {
        var scope = new RestHealScope();
        Current.Value = scope;
        return scope;
    }
    internal static void Prepare(object creature, Action<int> record)
    {
        if (Current.Value != null) Current.Value._candidates[creature] = record;
    }
    internal static Action<int>? Take(object creature)
    {
        return Current.Value != null && Current.Value._candidates.Remove(creature, out var record) ? record : null;
    }
    public void Dispose()
    {
        if (_restored) return;
        Current.Value = _previous;
        _restored = true;
    }
}
