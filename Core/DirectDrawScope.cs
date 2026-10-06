using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RelicStats.Core;

// The scope flows into a relic's async work, but its caller is restored as soon as the
// relic returns its Task. Only the first draw belongs to that relic; nested draw hooks
// have the same scope and must not claim it again.
internal sealed class DirectDrawScope : IDisposable
{
    private static readonly AsyncLocal<DirectDrawScope?> Current = new();
    private readonly DirectDrawScope? _previous;
    private readonly object _player;
    private readonly Action<int> _count;
    private readonly EffectScope<int> _effect;
    private bool _claimed;
    private bool _restored;

    private DirectDrawScope(object player, Action<int> count, bool firstOnly)
    {
        _previous = Current.Value;
        _player = player;
        _count = count;
        _effect = EffectScope<int>.Begin(player, count, firstOnly);
    }

    public static DirectDrawScope Begin(object player, Action<int> count, bool firstOnly = true)
    {
        var scope = new DirectDrawScope(player, count, firstOnly);
        Current.Value = scope;
        return scope;
    }

    public static DirectDrawScope? Claim(object player)
    {
        var scope = Current.Value;
        if (scope == null || scope._claimed || !ReferenceEquals(scope._player, player)) return null;
        scope._claimed = true;
        return scope;
    }

    public async Task<IEnumerable<T>> CountWhenDone<T>(Task<IEnumerable<T>> draw)
    {
        var cards = await draw;
        int count = cards.Count();
        if (count > 0) _count(count);
        return cards;
    }

    internal static EffectScope<int>.Lease? EnterCommand(object player) =>
        EffectScope<int>.EnterCommand(player);

    internal static async Task<IEnumerable<T>> CountCommand<T>(Task<IEnumerable<T>> draw,
        EffectScope<int>.Lease lease)
    {
        var cards = await draw;
        await lease.Observe(Task.FromResult(cards.Count()));
        return cards;
    }

    public void Dispose()
    {
        if (_restored) return;
        _effect.Dispose();
        Current.Value = _previous;
        _restored = true;
    }
}
