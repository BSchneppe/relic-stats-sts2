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
                ["relics"] = SerializeRelics(),
                ["floorMelted"] = SerializeFloorMelted(),
            };

            DirAccess.MakeDirRecursiveAbsolute(SaveDir);
            var path = isMultiplayer ? MultiplayerPath : SingleplayerPath;
            var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            file?.StoreString(json);
            file?.Close();
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

            DeserializeRelics(root["relics"]?.AsObject());
            DeserializeFloorMelted(root["floorMelted"]?.AsObject());
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

    private static JsonObject SerializeFloorMelted()
    {
        var obj = new JsonObject();
        foreach (var (id, floor) in RelicStatsRegistry.AllFloorMelted)
        {
            obj[id] = floor;
        }
        return obj;
    }

    public static string? GetSavedDescription(string relicId)
    {
        try
        {
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

        var stats = RelicStatsRegistry.Get(relicId);
        if (stats == null) return null;

        var previousState = stats.Save();
        try
        {
            stats.Load(relicData);
            // History display: no map history available, show raw stat with 1/1 denominators.
            return stats.GetDescription(1, 1);
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

    private static void DeserializeFloorMelted(JsonObject? floorMelted)
    {
        if (floorMelted == null) return;
        foreach (var (id, node) in floorMelted)
        {
            var floor = node?.GetValue<int>();
            if (floor.HasValue)
                RelicStatsRegistry.SetFloorMelted(id, floor.Value);
        }
    }
}
