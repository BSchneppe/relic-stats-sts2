using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace RelicStats.Core;

// Generation is an offer. Only a successful acquisition consumes its provenance, once.
internal sealed class CardAcquisitionLedger<TCard, TSource> where TCard : class where TSource : class
{
    private sealed class Entry
    {
        public IReadOnlyList<TSource> Sources = System.Array.Empty<TSource>();
        public bool Acquired;
    }

    private readonly ConditionalWeakTable<TCard, Entry> _entries = new();

    public void Record(TCard card, IEnumerable<TSource> sources)
    {
        var entry = _entries.GetValue(card, static _ => new Entry());
        if (!entry.Acquired) entry.Sources = sources.Distinct().ToArray();
    }

    public IReadOnlyList<TSource> Acquire(TCard card, bool success)
    {
        if (!success || !_entries.TryGetValue(card, out var entry) || entry.Acquired)
            return System.Array.Empty<TSource>();
        entry.Acquired = true;
        return entry.Sources;
    }
}
