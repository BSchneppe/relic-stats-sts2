using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;

namespace RelicStats.Core;

public static class StatsPersistence
{
    private static string SaveDir => OS.GetUserDataDir() + "/RelicStats/";
    private static string SingleplayerPath => SaveDir + "singleplayer.json";
    private static string MultiplayerPath => SaveDir + "multiplayer.json";

    public static void Save(bool isMultiplayer)
    {
        try
        {
            var root = new JsonObject
            {
                ["turnCount"] = RelicStatsRegistry.TurnCount,
                ["combatCount"] = RelicStatsRegistry.CombatCount,
                ["relics"] = SerializeRelics(),
            };

            DirAccess.MakeDirRecursiveAbsolute(SaveDir);
            var path = isMultiplayer ? MultiplayerPath : SingleplayerPath;
            var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            FileAccess.Open(path, FileAccess.ModeFlags.Write)?.StoreString(json);
        }
        catch (Exception e)
        {
            MainFile.Logger.Info($"Failed to save stats: {e.Message}");
        }
    }

    public static void Load(bool isMultiplayer)
    {
        try
        {
            var path = isMultiplayer ? MultiplayerPath : SingleplayerPath;
            if (!FileAccess.FileExists(path)) return;

            var json = FileAccess.Open(path, FileAccess.ModeFlags.Read)?.GetAsText();
            if (string.IsNullOrEmpty(json)) return;

            var root = JsonNode.Parse(json)?.AsObject();
            if (root == null) return;

            RelicStatsRegistry.TurnCount = root["turnCount"]?.GetValue<int>() ?? 0;
            RelicStatsRegistry.CombatCount = root["combatCount"]?.GetValue<int>() ?? 0;
            DeserializeRelics(root["relics"]?.AsObject());
        }
        catch (Exception e)
        {
            MainFile.Logger.Info($"Failed to load stats: {e.Message}");
        }
    }

    private static JsonObject SerializeRelics()
    {
        var relics = new JsonObject();
        foreach (var (id, stats) in RelicStatsRegistry.All)
        {
            relics[id] = stats.Save();
        }
        return relics;
    }

    public static string? GetSavedDescription(string relicId)
    {
        try
        {
            // Try singleplayer first, then multiplayer
            return GetSavedDescriptionFromFile(SingleplayerPath, relicId)
                ?? GetSavedDescriptionFromFile(MultiplayerPath, relicId);
        }
        catch (Exception e)
        {
            MainFile.Logger.Info($"Failed to load saved description: {e.Message}");
            return null;
        }
    }

    private static string? GetSavedDescriptionFromFile(string path, string relicId)
    {
        if (!FileAccess.FileExists(path)) return null;
        var json = FileAccess.Open(path, FileAccess.ModeFlags.Read)?.GetAsText();
        if (string.IsNullOrEmpty(json)) return null;

        var root = JsonNode.Parse(json)?.AsObject();
        if (root == null) return null;

        var relicData = root["relics"]?[relicId]?.AsObject();
        if (relicData == null) return null;

        // Load saved data into the registry's stats object temporarily to use its formatting
        var stats = RelicStatsRegistry.Get(relicId);
        if (stats == null) return null;

        var previousState = stats.Save();
        try
        {
            stats.Load(relicData);
            var totalTurns = root["turnCount"]?.GetValue<int>() ?? 0;
            var totalCombats = root["combatCount"]?.GetValue<int>() ?? 0;
            return stats.GetDescription(totalTurns, totalCombats);
        }
        finally
        {
            stats.Load(previousState);
        }
    }

    private static void DeserializeRelics(JsonObject? relics)
    {
        if (relics == null) return;
        foreach (var (id, node) in relics)
        {
            var stats = RelicStatsRegistry.Get(id);
            if (stats != null && node is JsonObject data)
            {
                stats.Load(data);
            }
        }
    }
}
