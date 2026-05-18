using System;
using System.Linq;
using DailyQuest.Models;
using ProjectM.Network;
using Unity.Collections;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;

namespace DailyQuest.Services;

internal static partial class QuestService
{
    public static string BuildStatusText(ulong sid, string playerName, Entity characterEntity = default)
    {
        EnsureAssignedForToday(sid, playerName, characterEntity);

        lock (_lock)
        {
            var key = sid.ToString();
            if (!_players.TryGetValue(key, out var st) || st == null)
                return "<color=red>No data.</color>";

            var easyQuest = GetQuestById_NoLock(st.EasyQuestId);
            var mediumQuest = GetQuestById_NoLock(st.MediumQuestId);
            var hardQuest = GetQuestById_NoLock(st.HardQuestId);

            string easyBlock = BuildQuestBlock_NoLock("1", easyQuest, st.EasyProgress, st.EasyClaimed);
            string mediumBlock = BuildQuestBlock_NoLock("2", mediumQuest, st.MediumProgress, st.MediumClaimed);
            string hardBlock = BuildQuestBlock_NoLock("3", hardQuest, st.HardProgress, st.HardClaimed);

            return
                $"<color=yellow>Daily Quests (Reset in {GetNextResetText()})</color>\n" +
                easyBlock + "\n" +
                mediumBlock + "\n" +
                hardBlock;
        }
    }

    private static string BuildQuestBlock_NoLock(string label, QuestDef quest, int progress, bool claimed)
    {
        if (quest == null)
            return $"<color=#87CEFA>Quest {label}</color>: <color=yellow>No quest {label.ToLowerInvariant()} configured.</color>";

        int need = Math.Max(0, quest.RequiredKills);
        int prog = Math.Max(0, progress);
        if (prog > need) prog = need;

        string rewardText = GetRewardDisplay_NoLock(quest);

        bool done = need > 0 && prog >= need;
        string statusText = done
            ? (claimed
                ? "Reward claimed"
                : "Completed! Claim with .quest reward")
            : $"Progress {prog}/{need}";

        return
            $"<color=#87CEFA>Quest {label}</color>: {quest.Name} x{need}\n" +
            $"Reward: {rewardText}\n" +
            $"Status: {statusText}";
    }

    public static string BuildPlayerStatusTextByName(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
            return "<color=red>Invalid player name.</color>";

        lock (_lock)
        {
            EnsureInitialized_NoLock();

            var st = _players.Values.FirstOrDefault(x =>
                x != null &&
                !string.IsNullOrWhiteSpace(x.Name) &&
                string.Equals(x.Name, playerName, StringComparison.OrdinalIgnoreCase));

            if (st == null)
                return $"<color=red>Player not found in quest data.</color>";

            var easyQuest = GetQuestById_NoLock(st.EasyQuestId);
            var mediumQuest = GetQuestById_NoLock(st.MediumQuestId);
            var hardQuest = GetQuestById_NoLock(st.HardQuestId);

            string easyBlock = BuildAdminQuestLine_NoLock("1", easyQuest, st.EasyProgress, st.EasyClaimed);
            string mediumBlock = BuildAdminQuestLine_NoLock("2", mediumQuest, st.MediumProgress, st.MediumClaimed);
            string hardBlock = BuildAdminQuestLine_NoLock("3", hardQuest, st.HardProgress, st.HardClaimed);

            string today = DateTime.Now.ToString("yyyy-MM-dd");
            bool isToday = string.Equals(st.Date, today, StringComparison.Ordinal);

            string header = $"<color=yellow>Daily Quests: <color=white>{st.Name}</color></color>";
            string dateLine = isToday
                ? $"<color=#87CEFA>Quest Date</color>: {st.Date}"
                : $"<color=#87CEFA>Quest Date</color>: {st.Date} (Outdated)";

            return
                header + "\n" +
                easyBlock + "\n" +
                mediumBlock + "\n" +
                hardBlock + "\n" +
                dateLine;
        }
    }

    private static string BuildAdminQuestLine_NoLock(string label, QuestDef quest, int progress, bool claimed)
    {
        if (quest == null)
            return $"<color=#87CEFA>Quest {label}</color>: <color=yellow>No quest configured.</color>";

        int need = Math.Max(0, quest.RequiredKills);
        int prog = Math.Max(0, progress);
        if (prog > need) prog = need;

        string status = claimed
            ? "(Reward claimed)"
            : prog >= need
                ? "(Completed)"
                : $"({prog}/{need})";

        string questName = string.IsNullOrWhiteSpace(quest.Name) ? $"Quest {label}" : quest.Name;

        return $"<color=#87CEFA>Quest {label}</color>: {questName} {status}";
    }

    private static bool TryClaimOne_NoLock(Entity userEntity, Entity characterEntity, string label, string questId, int progress, bool alreadyClaimed, out string replyText, out PrefabGUID rewardPrefabOut, out int rewardAmountOut)
    {
        replyText = "";
        rewardPrefabOut = new PrefabGUID(0);
        rewardAmountOut = 0;

        if (string.IsNullOrWhiteSpace(questId))
        {
            replyText = $"<color=#87CEFA>Quest {label}</color>: not assigned today."; return false;
        }

        var quest = GetQuestById_NoLock(questId);
        if (quest == null)
        {
            replyText = $"<color=#87CEFA>Quest {label}</color>: not found in config."; return false;
        }

        int need = Math.Max(0, quest.RequiredKills);
        if (need <= 0)
        {
            replyText = $"<color=#87CEFA>Quest {label}</color>: has invalid requiredKills."; return false;
        }

        if (progress < need)
        {
            replyText = $"<color=#87CEFA>Quest {label}</color>: not completed yet. Use <color=green>.quest daily</color> to check."; return false;
        }

        if (alreadyClaimed)
        {
            replyText = $"<color=#87CEFA>Quest {label}</color>: reward already claimed today."; return false;
        }

        if (!TryResolveRewardPrefab_NoLock(quest, out var rewardPrefab, out var rewardName))
        {
            replyText = "<color=red>Reward prefab not configured correctly.</color>"; return false;
        }

        int amount = Math.Max(0, quest.Reward?.Amount ?? 0);
        if (amount <= 0)
        {
            replyText = "<color=red>Reward amount invalid.</color>"; return false;
        }

        rewardPrefabOut = rewardPrefab;
        rewardAmountOut = amount;

        replyText = $"<color=#87CEFA>Quest {label}</color>: reward claimed <color=#87CEFA>{amount}x {rewardName}</color>";
        return true;
    }

    private static void SendQuestToast(User user, string label, string questName, int prog, int need, bool done)
    {
        if (user == default || !user.IsConnected)
            return;

        if (need < 0) need = 0;
        if (prog < 0) prog = 0;
        if (prog > need) prog = need;

        string status = done
            ? $"<color=green>{prog}/{need}</color>, Claim: <color=green>.quest reward</color>"
            : $"<color=yellow>{prog}/{need}</color>, Details: <color=green>.quest daily</color>";

        string msg = $"<color=#87CEFA>Quest {label}</color>: <color=white>{questName}</color> {status}";

        var fs = new FixedString512Bytes(msg);
        ServerChatUtils.SendSystemMessageToClient(Core.EntityManager, user, ref fs);
    }

    private static string GetRewardDisplay_NoLock(QuestDef quest)
    {
        if (quest?.Reward == null) return "None";

        string name = quest.Reward.Name ?? "";
        int amount = quest.Reward.Amount;

        if (string.IsNullOrWhiteSpace(name))
        {
            int prefab = quest.Reward.Prefab;
            if (prefab != 0)
            {
                try { name = new PrefabGUID(prefab).LookupName(); }
                catch { name = prefab.ToString(); }
            }
            else
            {
                name = "Unknown";
            }
        }

        return $"{Math.Max(0, amount)}x {name}";
    }
}
