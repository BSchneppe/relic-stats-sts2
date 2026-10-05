using System;
using System.Collections.Generic;
using System.Threading;

namespace RelicStats.Core;

internal sealed class ChipDiscardScope : IDisposable
{
    private static readonly AsyncLocal<ChipDiscardScope?> Current = new();
    private static readonly AsyncLocal<Command?> ActiveCommand = new();
    private readonly ChipDiscardScope? _previous;
    private readonly object _owner;
    private readonly Action _record;
    private bool _restored;

    private ChipDiscardScope(object owner, Action record)
    {
        _previous = Current.Value;
        _owner = owner;
        _record = record;
    }

    internal static ChipDiscardScope Begin(object owner, Action record)
    {
        var scope = new ChipDiscardScope(owner, record);
        Current.Value = scope;
        return scope;
    }

    internal static IDisposable? EnterCommand(object owner, IEnumerable<object> selected)
    {
        var source = Current.Value;
        if (source == null || !ReferenceEquals(source._owner, owner)) return null;
        var previous = ActiveCommand.Value;
        var command = new Command(source, selected, previous, previous?.Source != source);
        ActiveCommand.Value = command;
        return command;
    }

    internal static void Discarded(object card)
    {
        var command = ActiveCommand.Value;
        if (command?.Direct == true && command.Selected.Remove(card)) command.Source._record();
    }

    public void Dispose()
    {
        if (_restored) return;
        Current.Value = _previous;
        _restored = true;
    }

    private sealed class Command : IDisposable
    {
        internal readonly ChipDiscardScope Source;
        internal readonly HashSet<object> Selected;
        internal readonly bool Direct;
        private readonly Command? _previous;
        private bool _restored;
        internal Command(ChipDiscardScope source, IEnumerable<object> selected, Command? previous, bool direct)
        {
            Source = source;
            Selected = new HashSet<object>(selected, ReferenceEqualityComparer.Instance);
            Direct = direct;
            _previous = previous;
        }
        public void Dispose()
        {
            if (_restored) return;
            ActiveCommand.Value = _previous;
            _restored = true;
        }
    }
}
