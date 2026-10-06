using System;
using System.Threading;

namespace RelicStats.Core;

// A source can issue several plays, but nested plays belong to their own effects.
internal sealed class AutoPlayScope : IDisposable
{
    private static readonly AsyncLocal<AutoPlayScope?> Current = new();
    private static readonly AsyncLocal<Play?> CurrentPlay = new();
    private readonly AutoPlayScope? _previous;
    private readonly object _owner;
    private readonly Action _record;
    private bool _restored;

    private AutoPlayScope(object owner, Action record)
    {
        _previous = Current.Value;
        _owner = owner;
        _record = record;
    }

    public static AutoPlayScope Begin(object owner, Action record)
    {
        var scope = new AutoPlayScope(owner, record);
        Current.Value = scope;
        return scope;
    }

    public static IDisposable? EnterCommand(object owner, object card)
    {
        var source = Current.Value;
        if (source == null || !ReferenceEquals(source._owner, owner)) return null;
        var previous = CurrentPlay.Value;
        var play = new Play(source, card, previous, previous?.Source != source);
        CurrentPlay.Value = play;
        return play;
    }

    public static void Finished(object card, bool firstPlay)
    {
        var play = CurrentPlay.Value;
        if (play == null || !play.Direct || play.Recorded || !firstPlay ||
            !ReferenceEquals(play.Card, card)) return;
        play.Recorded = true;
        play.Source._record();
    }

    public void Dispose()
    {
        if (_restored) return;
        Current.Value = _previous;
        _restored = true;
    }

    private sealed class Play : IDisposable
    {
        internal readonly AutoPlayScope Source;
        internal readonly object Card;
        internal readonly bool Direct;
        internal bool Recorded;
        private readonly Play? _previous;
        private bool _restored;

        internal Play(AutoPlayScope source, object card, Play? previous, bool direct)
        {
            Source = source;
            Card = card;
            _previous = previous;
            Direct = direct;
        }

        public void Dispose()
        {
            if (_restored) return;
            CurrentPlay.Value = _previous;
            _restored = true;
        }
    }
}
