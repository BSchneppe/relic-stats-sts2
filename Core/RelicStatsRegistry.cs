using System;
using System.Collections.Generic;
using System.Reflection;

namespace RelicStats.Core;

public static class RelicStatsRegistry
{
    private static readonly Dictionary<string, IRelicStats> _stats = new();

    public static int TurnCount { get; set; }
    public static int CombatCount { get; set; }

    public static void DiscoverAndRegister()
    {
        _stats.Clear();
        TurnCount = 0;
        CombatCount = 0;

        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
        {
            if (typeof(IRelicStats).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
            {
                var instance = (IRelicStats)Activator.CreateInstance(type)!;
                _stats[instance.RelicId] = instance;
            }
        }

        MainFile.Logger.Info($"Registered stats for {_stats.Count} relics");
    }

    public static IRelicStats? Get(string relicId)
    {
        return _stats.GetValueOrDefault(relicId);
    }

    public static IReadOnlyDictionary<string, IRelicStats> All => _stats;

    public static void ResetAll()
    {
        TurnCount = 0;
        CombatCount = 0;
        foreach (var stats in _stats.Values)
        {
            stats.Reset();
        }
    }
}
