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
