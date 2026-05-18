using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DailyQuest.Config;
using DailyQuest.Models;

namespace DailyQuest.Services;

internal static partial class QuestService
{
    public static void EnsureFilesExist()
    {
        try
        {
            Directory.CreateDirectory(CONFIG_DIR);

            if (!File.Exists(CONFIG_FILE))
            {
                File.WriteAllText(CONFIG_FILE, DefaultQuestConfig.QuestConfigJson);
                Core.Log.LogInfo($"[Quest] Created config: {CONFIG_FILE}");
            }

            if (!File.Exists(PLAYER_FILE))
            {
                File.WriteAllText(PLAYER_FILE, "{}");
                Core.Log.LogInfo($"[Quest] Created player data: {PLAYER_FILE}");
            }
        }
        catch (Exception e)
        {
            Core.LogException(e);
        }
    }

    private static void RebuildQuestIndex_NoLock()
    {
        _questsById = new Dictionary<string, QuestDef>(StringComparer.Ordinal);
        _allQuests = new List<QuestDef>();
        _easyQuests = new List<QuestDef>();
        _mediumQuests = new List<QuestDef>();
        _hardQuests = new List<QuestDef>();

        _allActiveTargetPrefabs = new HashSet<int>();

        if (_config?.Quests == null) return;

        foreach (var q in _config.Quests)
        {
            if (q == null) continue;

            if (!string.IsNullOrWhiteSpace(q.Id))
                _questsById[q.Id] = q;

            _allQuests.Add(q);

            if (q.TargetPrefabs != null)
            {
                foreach (var prefab in q.TargetPrefabs)
                {
                    _allActiveTargetPrefabs.Add(prefab);
                }
            }

            var diff = (q.Difficulty ?? "easy").Trim().ToLowerInvariant();
            if (diff == "hard")
                _hardQuests.Add(q);
            else if (diff == "medium")
                _mediumQuests.Add(q);
            else
                _easyQuests.Add(q);
        }
    }

    private static void LoadConfig_NoLock()
    {
        try
        {
            string json = File.ReadAllText(CONFIG_FILE);            
            var questList = JsonSerializer.Deserialize<List<QuestDef>>(json, JsonOpts);
            _config = new QuestConfig { Quests = questList ?? new List<QuestDef>() };
            
            RebuildQuestIndex_NoLock();
        }
        catch (Exception e)
        {
            Core.LogException(e);
            _config = new QuestConfig();
            RebuildQuestIndex_NoLock();
        }
    }

    private static void LoadPlayers_NoLock()
    {
        try
        {
            if (!File.Exists(PLAYER_FILE))
            {
                _players = new Dictionary<string, PlayerQuestState>();
                return;
            }

            string json = File.ReadAllText(PLAYER_FILE);
            if (string.IsNullOrWhiteSpace(json))
            {
                _players = new Dictionary<string, PlayerQuestState>();
                return;
            }

            _players = JsonSerializer.Deserialize<Dictionary<string, PlayerQuestState>>(json, JsonOpts)
                       ?? new Dictionary<string, PlayerQuestState>();
        }
        catch (Exception e)
        {
            Core.LogException(e);
            _players = new Dictionary<string, PlayerQuestState>();
        }
    }

    private static void SavePlayers_NoLock()
    {
        try
        {
            Directory.CreateDirectory(CONFIG_DIR);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(_players, JsonOpts);
            File.WriteAllBytes(PLAYER_FILE, bytes);
        }
        catch (Exception e)
        {
            Core.LogException(e);
        }
    }
}
