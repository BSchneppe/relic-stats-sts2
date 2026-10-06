using System;
using System.Threading;
using System.Threading.Tasks;

namespace RelicStats.Core;

// Attribution follows the source's async work. Entering a command marks its captured
// context, so nested commands cannot count as another direct effect of that source.
internal sealed class EffectScope<T> : IDisposable
{
    private static readonly AsyncLocal<Frame?> Current = new();
    private readonly Frame? _previous;
    private readonly object _owner;
    private readonly Action<T> _record;
    private readonly bool _firstOnly;
    private bool _claimed;
    private bool _restored;

    internal sealed record Frame(EffectScope<T> Scope, bool InCommand);

    private EffectScope(object owner, Action<T> record, bool firstOnly)
    {
        _previous = Current.Value;
        _owner = owner;
        _record = record;
        _firstOnly = firstOnly;
    }

    public static object? CurrentOwner => Current.Value?.Scope._owner;

    public static EffectScope<T> Begin(object owner, Action<T> record, bool firstOnly = false)
    {
        var scope = new EffectScope<T>(owner, record, firstOnly);
        Current.Value = new Frame(scope, false);
        return scope;
    }

    public static Lease? EnterCommand(object owner)
    {
        var frame = Current.Value;
        if (frame == null || frame.InCommand || !ReferenceEquals(frame.Scope._owner, owner)) return null;
        var scope = frame.Scope;
        if (scope._firstOnly && scope._claimed) return null;
        scope._claimed = true;
        Current.Value = new Frame(scope, true);
        return new Lease(scope, frame);
    }

    public void Dispose()
    {
        if (_restored) return;
        Current.Value = _previous;
        _restored = true;
    }

    internal sealed class Lease : IDisposable
    {
        private readonly EffectScope<T> _scope;
        private readonly Frame _previous;
        private bool _restored;

        internal Lease(EffectScope<T> scope, Frame previous)
        {
            _scope = scope;
            _previous = previous;
        }

        public async Task<T> Observe(Task<T> command)
        {
            var result = await command;
            _scope._record(result);
            return result;
        }

        public void Dispose()
        {
            if (_restored) return;
            Current.Value = _previous;
            _restored = true;
        }
    }
}
