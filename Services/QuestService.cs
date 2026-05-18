using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Entities;
using UnityEngine;
using VampireCommandFramework;
using Unity.Collections;
using DailyQuest.Models;

namespace DailyQuest.Services;

internal static partial class QuestService
{
    private static readonly string CONFIG_DIR = Path.Combine(BepInEx.Paths.ConfigPath, MyPluginInfo.PLUGIN_NAME);
    private static readonly string CONFIG_FILE = Path.Combine(CONFIG_DIR, "quest_config.json");
    private static readonly string PLAYER_FILE = Path.Combine(CONFIG_DIR, "quest_player.json");

    private static readonly object _lock = new();

    private static QuestConfig _config = new();
    private static Dictionary<string, QuestDef> _questsById = new(StringComparer.Ordinal);
    private static List<QuestDef> _allQuests = new();
    private static List<QuestDef> _easyQuests = new();
    private static List<QuestDef> _mediumQuests = new();
    private static List<QuestDef> _hardQuests = new();
    private static Dictionary<string, PlayerQuestState> _players = new();

    private static HashSet<int> _allActiveTargetPrefabs = new();

    private static string _cachedTodayString = DateTime.Now.ToString("yyyy-MM-dd");
    private static string TodayString() => _cachedTodayString;

    private static DateTime _lastDate = DateTime.MinValue.Date;
    private static bool _initialized;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true
    };

    public static bool IsQuestTarget(int prefabHash)
    {
        return _allActiveTargetPrefabs.Contains(prefabHash);
    }

    public static void Initialize()
    {
        lock (_lock)
        {
            EnsureInitialized_NoLock();
        }

     // Core.Log.LogInfo("[Quest] QuestService initialized");
    }

    private static void EnsureInitialized_NoLock()
    {
        if (_initialized)
            return;

        EnsureFilesExist(); 
        LoadConfig_NoLock();
        LoadPlayers_NoLock();

        SaveThrottle.Init(() => SavePlayers_NoLock(), TimeSpan.FromSeconds(10));

        _lastDate = DateTime.Now.Date;
        _initialized = true;

     // Core.Log.LogInfo("[Quest] QuestService EnsureInitialized");
    }

    public static void Reload()
    {
        lock (_lock)
        {
            if (Plugin.PluginConfig != null)
            {
                Plugin.PluginConfig.Reload();
            }

            EnsureFilesExist();
            LoadConfig_NoLock();
            _initialized = true;
        }

        Core.Log.LogInfo("[Quest] DailyQuest.cfg and quest_config.json reloaded successfully");
    }

    public static void EnsureAssignedForToday(ulong sid, string playerName, Entity characterEntity = default)
    {
        if (sid == 0) return;

        lock (_lock)
        {
            EnsureInitialized_NoLock();
            RollDateIfNeeded_NoLock();

            string today = TodayString();
            var key = sid.ToString();
            bool changed = false;

            if (!_players.TryGetValue(key, out var st) || st == null)
            {
                st = new PlayerQuestState
                {
                    SteamId = sid,
                    Name = playerName ?? "",
                    Date = today,

                    EasyQuestId = "",
                    EasyProgress = 0,
                    EasyClaimed = false,

                    MediumQuestId = "",
                    MediumProgress = 0,
                    MediumClaimed = false,

                    HardQuestId = "",
                    HardProgress = 0,
                    HardClaimed = false
                };
                changed = true;
            }
            else
            {
                var newName = playerName ?? st.Name ?? "";
                if (!string.Equals(st.Name, newName, StringComparison.Ordinal))
                {
                    st.Name = newName;
                    changed = true;
                }
            }

            bool dayChanged = !string.Equals(st.Date, today, StringComparison.Ordinal);
            if (dayChanged)
            {
                st.Date = today;

                st.EasyQuestId = "";
                st.EasyProgress = 0;
                st.EasyClaimed = false;

                st.MediumQuestId = "";
                st.MediumProgress = 0;
                st.MediumClaimed = false;

                st.HardQuestId = "";
                st.HardProgress = 0;
                st.HardClaimed = false;

                changed = true;

                if (Helper.TryRemoveDailyQuestBuff(characterEntity, st))
                {
                    changed = true;
                }
            }

            if (string.IsNullOrWhiteSpace(st.EasyQuestId))
            {
                st.EasyQuestId = PickQuestId_NoLock(sid, today, "easy", _easyQuests);
                st.EasyProgress = 0;
                st.EasyClaimed = false;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(st.MediumQuestId))
            {
                st.MediumQuestId = PickQuestId_NoLock(sid, today, "medium", _mediumQuests);
                st.MediumProgress = 0;
                st.MediumClaimed = false;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(st.HardQuestId))
            {
                st.HardQuestId = PickQuestId_NoLock(sid, today, "hard", _hardQuests);
                st.HardProgress = 0;
                st.HardClaimed = false;
                changed = true;
            }

            if (changed)
            {
                _players[key] = st;
                SaveThrottle.MarkDirty();
            }
        }
    }

    public static bool TryClaim(ChatCommandContext ctx, ulong sid, string playerName, Entity characterEntity)
    {
        EnsureAssignedForToday(sid, playerName);

        lock (_lock)
        {
            var key = sid.ToString();
            if (!_players.TryGetValue(key, out var st) || st == null)
            {
                ctx.Reply("<color=red>No quest data.</color>");
                return false;
            }

            var userEntity = ctx.Event.SenderUserEntity;
            bool claimedAny = false;
            var claimedNames = new List<string>(3);
            var pendingRewards = new List<(PrefabGUID prefab, int amount)>();

            if (TryClaimOne_NoLock(userEntity, characterEntity, "1", st.EasyQuestId, st.EasyProgress, st.EasyClaimed, out var replyEasy, out var prefab1, out var amount1))
            {
                st.EasyClaimed = true;
                claimedAny = true;
                pendingRewards.Add((prefab1, amount1));

                if (Plugin.GearRepairOnClaimEnabled != null && Plugin.GearRepairOnClaimEnabled.Value) Helper.RepairAmulet(characterEntity);
                
                var q = GetQuestById_NoLock(st.EasyQuestId);
                int need = Math.Max(0, q?.RequiredKills ?? 0);
                claimedNames.Add(q != null ? $"{q.Name} x{need}" : "Quest 1");
            }

            if (TryClaimOne_NoLock(userEntity, characterEntity, "2", st.MediumQuestId, st.MediumProgress, st.MediumClaimed, out var replyMedium, out var prefab2, out var amount2))
            {
                st.MediumClaimed = true;
                claimedAny = true;
                pendingRewards.Add((prefab2, amount2));

                if (Plugin.GearRepairOnClaimEnabled != null && Plugin.GearRepairOnClaimEnabled.Value) Helper.RepairArmor(characterEntity);

                var q = GetQuestById_NoLock(st.MediumQuestId);
                int need = Math.Max(0, q?.RequiredKills ?? 0);
                claimedNames.Add(q != null ? $"{q.Name} x{need}" : "Quest 2");
            }

            if (TryClaimOne_NoLock(userEntity, characterEntity, "3", st.HardQuestId, st.HardProgress, st.HardClaimed, out var replyHard, out var prefab3, out var amount3))
            {
                st.HardClaimed = true;
                claimedAny = true;
                pendingRewards.Add((prefab3, amount3));

                if (Plugin.GearRepairOnClaimEnabled != null && Plugin.GearRepairOnClaimEnabled.Value) Helper.RepairWeapon(characterEntity);

                var q = GetQuestById_NoLock(st.HardQuestId);
                int need = Math.Max(0, q?.RequiredKills ?? 0);
                claimedNames.Add(q != null ? $"{q.Name} x{need}" : "Quest 3");
            }

            string finalMessage = 
                $"<color=yellow>Daily Quests (Reset in {GetNextResetText()})</color>\n" +
                replyEasy + "\n" +
                replyMedium + "\n" +
                replyHard;

            if (!claimedAny)
            {
                ctx.Reply(finalMessage);
                return false;
            }

            foreach (var reward in pendingRewards)
            {
                Helper.AddItemToInventory(characterEntity, reward.prefab, reward.amount);
            }

            Helper.TrySendBroadcastMessage(playerName, claimedNames);
            Helper.TrySendWebhookMessage(playerName, claimedNames);
            Helper.TryAddDailyQuestBuff(userEntity, characterEntity, st);

            _players[key] = st;
            SaveThrottle.ForceSave();

            ctx.Reply(finalMessage);

            return true;
        }
    }

    public static void OnKilledPrefab(ulong sid, int diedPrefabGuidHash, User user, string playerNameForEnsure = "", Entity characterEntity = default)
    {
        if (sid == 0 || diedPrefabGuidHash == 0) return;

        EnsureAssignedForToday(sid, playerNameForEnsure, characterEntity);

        lock (_lock)
        {
            var key = sid.ToString();
            if (!_players.TryGetValue(key, out var st) || st == null)
                return;

            bool changed = false;

            var easyQuest = GetQuestById_NoLock(st.EasyQuestId);
            if (easyQuest != null &&
                easyQuest.TargetPrefabs != null &&
                easyQuest.TargetPrefabs.Length > 0 &&
                easyQuest.TargetPrefabs.Contains(diedPrefabGuidHash))
            {
                int need = Math.Max(0, easyQuest.RequiredKills);
                if (need > 0 && st.EasyProgress < need)
                {
                    st.EasyProgress++;
                    if (st.EasyProgress > need) st.EasyProgress = need;
                    changed = true;

                    SendQuestToast(user, "1", easyQuest.Name, st.EasyProgress, need, st.EasyProgress >= need);
                }
            }

            var mediumQuest = GetQuestById_NoLock(st.MediumQuestId);
            if (mediumQuest != null &&
                mediumQuest.TargetPrefabs != null &&
                mediumQuest.TargetPrefabs.Length > 0 &&
                mediumQuest.TargetPrefabs.Contains(diedPrefabGuidHash))
            {
                int need = Math.Max(0, mediumQuest.RequiredKills);
                if (need > 0 && st.MediumProgress < need)
                {
                    st.MediumProgress++;
                    if (st.MediumProgress > need) st.MediumProgress = need;
                    changed = true;

                    SendQuestToast(user, "2", mediumQuest.Name, st.MediumProgress, need, st.MediumProgress >= need);
                }
            }

            var hardQuest = GetQuestById_NoLock(st.HardQuestId);
            if (hardQuest != null &&
                hardQuest.TargetPrefabs != null &&
                hardQuest.TargetPrefabs.Length > 0 &&
                hardQuest.TargetPrefabs.Contains(diedPrefabGuidHash))
            {
                int need = Math.Max(0, hardQuest.RequiredKills);
                if (need > 0 && st.HardProgress < need)
                {
                    st.HardProgress++;
                    if (st.HardProgress > need) st.HardProgress = need;
                    changed = true;

                    SendQuestToast(user, "3", hardQuest.Name, st.HardProgress, need, st.HardProgress >= need);
                }
            }

            if (changed)
            {
                _players[key] = st;
                SaveThrottle.MarkDirty();
            }
        }
    }

    private static void RollDateIfNeeded_NoLock()
    {
        var nowDate = DateTime.Now.Date;
        if (nowDate == _lastDate) return;

        _lastDate = nowDate;
        _cachedTodayString = _lastDate.ToString("yyyy-MM-dd");
        
        Core.Log.LogInfo($"[Quest] New day detected: {_lastDate:yyyy-MM-dd}");
    }

    private static string GetNextResetText()
    {
        var now = DateTime.Now;
        var nextReset = now.Date.AddDays(1);

        var remaining = nextReset - now;
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        int hours = (int)remaining.TotalHours;
        return $"{hours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
    }

    private static QuestDef GetQuestById_NoLock(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        if (_questsById.TryGetValue(id, out var q))
            return q;

        return _config?.Quests?.FirstOrDefault(x => x != null && string.Equals(x.Id, id, StringComparison.Ordinal));
    }

    private static string PickQuestId_NoLock(ulong sid, string date, string difficulty, List<QuestDef> pool)
    {
        if (pool == null || pool.Count == 0)
            return "";

        int seed = HashCode.Combine(sid, date, difficulty);
        var rnd = new System.Random(seed);
        var pick = pool[rnd.Next(pool.Count)];
        return pick?.Id ?? "";
    }

    private static bool TryResolveRewardPrefab_NoLock(QuestDef quest, out PrefabGUID rewardPrefab, out string rewardName)
    {
        rewardPrefab = new PrefabGUID(0);
        rewardName = "Reward";

        if (quest?.Reward == null) return false;

        int prefabInt = quest.Reward.Prefab;
        if (prefabInt == 0) return false;

        rewardPrefab = new PrefabGUID(prefabInt);

        rewardName = quest.Reward.Name;
        if (string.IsNullOrWhiteSpace(rewardName))
        {
            try { rewardName = rewardPrefab.LookupName(); }
            catch { rewardName = prefabInt.ToString(); }
        }

        return true;
    }

    public static void AdminRemoveBuff(ChatCommandContext ctx, string targetPlayerName)
    {
        lock (_lock)
        {
            EnsureInitialized_NoLock();

            var targetState = _players.Values.FirstOrDefault(x => x != null && string.Equals(x.Name, targetPlayerName, StringComparison.OrdinalIgnoreCase));

            if (targetState == null)
            {
                ctx.Reply($"<color=red>Player {targetPlayerName} not found in quest data.</color>");
                return;
            }

            Entity charEntity = Entity.Null;
            var userEntities = Helper.GetEntitiesByComponentType<User>(); 
            
            try
            {
                foreach (var uEntity in userEntities)
                {
                    var user = uEntity.Read<User>(); 
                    if (user.PlatformId == targetState.SteamId) 
                    {
                        charEntity = user.LocalCharacter._Entity;
                        break;
                    }
                }
            }
            finally
            {
                if (userEntities.IsCreated) userEntities.Dispose();
            }

            if (charEntity == Entity.Null)
            {
                ctx.Reply($"<color=red>Could not find physical character for {targetState.Name}.</color>");
                return;
            }

            if (targetState.ClaimedBuffPrefab == 0)
            {
                int configBuffId = Plugin.ClaimedBuffPrefab?.Value ?? 0;
                if (configBuffId != 0)
                {
                    Buffs.RemoveBuff(charEntity, new PrefabGUID(configBuffId));
                    ctx.Reply($"<color=green>Force removed config buff from {targetState.Name}.</color>");
                }
                else
                {
                    ctx.Reply($"<color=yellow>Player {targetState.Name} has no active buff history.</color>");
                }
                return;
            }

            if (Helper.TryRemoveDailyQuestBuff(charEntity, targetState))
            {
                var key = targetState.SteamId.ToString();
                _players[key] = targetState; 
                SaveThrottle.ForceSave(); 

                ctx.Reply($"<color=green>Successfully removed daily quest buff from</color> <color=white>{targetState.Name}</color>.");
            }
            else
            {
                ctx.Reply($"<color=red>Failed to remove buff from {targetState.Name}.</color>");
            }
        }
    }
}
