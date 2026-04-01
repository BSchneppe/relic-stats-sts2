using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;

namespace RelicStats.Core;

public static class StatsPersistence
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    // ── Save store access (writes go through CloudSaveStore → Steam Cloud) ──

    private static readonly FieldInfo SaveStoreField =
        typeof(SaveManager).GetField("_saveStore", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static ISaveStore SaveStore => (ISaveStore)SaveStoreField.GetValue(SaveManager.Instance)!;

    // ── Path helpers (profile-relative, same tree as game saves) ──

    private static string ProfileDir =>
        UserDataPathProvider.GetProfileDir(SaveManager.Instance.CurrentProfileId);

    private static string ActiveRunPath(bool isMultiplayer) =>
        $"{ProfileDir}/{UserDataPathProvider.SavesDir}/" +
        (isMultiplayer ? "relicstats_mp.json" : "relicstats.json");

    private static string HistoryRunPath(long startTime) =>
        $"{ProfileDir}/{UserDataPathProvider.SavesDir}/history/relicstats/{startTime}.json";

    // ── Active run persistence ──

    public static void Save(bool isMultiplayer)
    {
        try
        {
            var json = BuildSaveObject().ToJsonString(WriteOptions);
            var store = SaveStore;
            var path = ActiveRunPath(isMultiplayer);
            store.WriteFile(path, json);
        }
        catch (Exception e)
        {
            MainFile.Logger.Info($"[StatsPersistence] Failed to save active run stats: {e.Message}");
        }
    }

    public static void Load(bool isMultiplayer)
    {
        try
        {
            SyncFromCloud(ActiveRunPath(isMultiplayer));
            TryLoadFrom(ActiveRunPath(isMultiplayer));
        }
        catch (Exception e)
        {
            MainFile.Logger.Info($"[StatsPersistence] Failed to load active run stats: {e.Message}");
        }
    }

    // ── Run history persistence ──

    /// <summary>
    /// Saves a snapshot of current relic stats to a per-run file in the history dir.
    /// Called when CreateRunHistoryEntry fires.
    /// </summary>
    public static void SaveRunHistory(long startTime)
    {
        try
        {
            var json = BuildSaveObject().ToJsonString(WriteOptions);
            var store = SaveStore;
            var dir = $"{ProfileDir}/{UserDataPathProvider.SavesDir}/history/relicstats";
            store.CreateDirectory(dir);
            store.WriteFile(HistoryRunPath(startTime), json);
        }
        catch (Exception e)
        {
            MainFile.Logger.Info($"[StatsPersistence] Failed to save run history stats: {e.Message}");
        }
    }

    /// <summary>
    /// Loads a relic's description from the per-run history file, deriving
    /// turns/combats from the RunHistory's MapPointHistory.
    /// </summary>
    public static string? GetRunHistoryDescription(
        string relicId, long startTime,
        List<List<MapPointHistoryEntry>> mapPointHistory, int floorAdded)
    {
        try
        {
            var historyPath = HistoryRunPath(startTime);
            SyncFromCloud(historyPath);
            var result = LoadDescriptionFromFile(historyPath, relicId, mapPointHistory, floorAdded);
            if (result != null) return result;

            // Fall back to active-run files for runs completed before per-run history existed
            return LoadDescriptionFromFile(ActiveRunPath(false), relicId, mapPointHistory, floorAdded)
                ?? LoadDescriptionFromFile(ActiveRunPath(true), relicId, mapPointHistory, floorAdded);
        }
        catch (Exception e)
        {
            MainFile.Logger.Info($"[StatsPersistence] Failed to load run history description: {e.Message}");
            return null;
        }
    }

    // ── Internals ──

    /// <summary>
    /// Pulls a single file from Steam Cloud to local if the cloud version is newer.
    /// Reads directly from the cloud store and writes locally, avoiding async deadlocks.
    /// No-op if the store is not a CloudSaveStore.
    /// </summary>
    private static void SyncFromCloud(string path)
    {
        try
        {
            if (SaveStore is not CloudSaveStore cloudStore) return;

            var cloud = cloudStore.CloudStore;
            var local = cloudStore.LocalStore;

            if (!cloud.FileExists(path)) return;

            var cloudTime = cloud.GetLastModifiedTime(path);
            if (local.FileExists(path))
            {
                var localTime = local.GetLastModifiedTime(path);
                if (cloudTime <= localTime) return;
            }

            var content = cloud.ReadFile(path);
            if (content != null)
            {
                local.WriteFile(path, content);
                local.SetLastModifiedTime(path, cloudTime);
            }
        }
        catch (Exception e)
        {
            MainFile.Logger.Info($"[StatsPersistence] Cloud sync failed for {path}: {e.Message}");
        }
    }

    private static JsonObject BuildSaveObject()
    {
        return new JsonObject
        {
            ["relics"] = SerializeRelics(),
            ["floorMelted"] = SerializeFloorMelted(),
        };
    }

    private static bool TryLoadFrom(string path)
    {
        var store = SaveStore;
        if (!store.FileExists(path)) return false;

        var json = store.ReadFile(path);
        if (string.IsNullOrEmpty(json)) return false;

        var root = JsonNode.Parse(json)?.AsObject();
        if (root == null) return false;

        DeserializeRelics(root["relics"]?.AsObject());
        DeserializeFloorMelted(root["floorMelted"]?.AsObject());
        return true;
    }

    private static string? LoadDescriptionFromFile(
        string path, string relicId,
        List<List<MapPointHistoryEntry>> mapPointHistory, int floorAdded)
    {
        var store = SaveStore;
        if (!store.FileExists(path)) return null;

        var json = store.ReadFile(path);
        if (string.IsNullOrEmpty(json)) return null;

        var root = JsonNode.Parse(json)?.AsObject();
        if (root == null) return null;

        var relicData = root["relics"]?[relicId]?.AsObject();
        if (relicData == null) return null;

        var stats = RelicStatsRegistry.Get(relicId);
        if (stats == null) return null;

        // Read floorMelted from save file to use as endFloor
        int? floorMelted = root["floorMelted"]?[relicId]?.GetValue<int>();
        var (turns, combats) = MapHistoryHelper.GetEffective(mapPointHistory, floorAdded, floorMelted);

        var previousState = stats.Save();
        try
        {
            stats.Load(relicData);
            return stats.GetDescription(turns, combats);
        }
        finally
        {
            stats.Load(previousState);
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
