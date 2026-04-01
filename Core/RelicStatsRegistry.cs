using System;
using System.Collections.Generic;
using System.Reflection;

namespace RelicStats.Core;

public static class RelicStatsRegistry
{
    private static readonly Dictionary<string, IRelicStats> _stats = new();
    private static readonly Dictionary<string, int> _floorMelted = new();

    public static void DiscoverAndRegister()
    {
        _stats.Clear();
        _floorMelted.Clear();

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

    public static void SetFloorMelted(string relicId, int floor) => _floorMelted[relicId] = floor;
    public static int? GetFloorMelted(string relicId) =>
        _floorMelted.TryGetValue(relicId, out var f) ? f : null;
    public static IReadOnlyDictionary<string, int> AllFloorMelted => _floorMelted;

    public static void ResetAll()
    {
        _floorMelted.Clear();
        foreach (var stats in _stats.Values)
        {
            stats.Reset();
        }
    }
}
