using DailyQuest.Services;
using Unity.Entities;
using VampireCommandFramework;
using System.Text.RegularExpressions;

namespace DailyQuest.Commands;

[CommandGroup("quest")]
internal static class QuestCommands
{

    [Command("daily", shortHand: "d", description: "Show your daily quests and progress.", adminOnly: false)]
    public static void Show(ChatCommandContext ctx)
    {
        var user = ctx.Event.User;
        ulong sid = user.PlatformId;
        string name = user.CharacterName.ToString();

        Entity character = ctx.Event.SenderCharacterEntity;

        var text = QuestService.BuildStatusText(sid, name, character);
        ctx.Reply(text);
    }

    [Command("reward", shortHand: "rw", description: "Claim all completed daily quest rewards.", adminOnly: false)]
    public static void Claim(ChatCommandContext ctx)
    {
        var user = ctx.Event.User;
        ulong sid = user.PlatformId;
        string name = user.CharacterName.ToString();

        Entity character = ctx.Event.SenderCharacterEntity;

        QuestService.TryClaim(ctx, sid, name, character);
    }

    [Command("reload", shortHand: "rl", description: "Reload quest_config.json", adminOnly: true)]
    public static void Reload(ChatCommandContext ctx)
    {
        QuestService.Reload();
        ctx.Reply("<color=green>Reloaded quest_config.json</color>");
    }
        
    [Command("info", shortHand: "i", description: "Show daily quest status for a specific player.", adminOnly: true)]
    public static void ShowPlayerQuest(ChatCommandContext ctx, string playerName)
    {
        var text = QuestService.BuildPlayerStatusTextByName(playerName);
        ctx.Reply(text);
    }

    [Command("debuff", shortHand: "db", description: "Force remove the daily quest buff from a player.", adminOnly: true)]
    public static void DebuffCommand(ChatCommandContext ctx, string playerName)
    {
        QuestService.AdminRemoveBuff(ctx, playerName);
    }

    [Command("testwebhook", shortHand: "tw", description: "Send a test message to Discord.", adminOnly: true)]
    public static void TestWebhook(ChatCommandContext ctx)
    {
        if (string.IsNullOrWhiteSpace(Plugin.WebhookUrl?.Value))
        {
            ctx.Reply("<color=red>Webhook URL is empty in config!</color>");
            return;
        }

        string testMsg = $"**[Daily quest]** - Webhook test message from {MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} by Del";
        
        ctx.Reply("<color=yellow>Send a webhook test message.</color>");

        _ = Helper.TrySendWebhookAsync(testMsg); 
    }

    [Command("config", shortHand: "cfg", description: "Display the DailyQuest configuration.", adminOnly: true)]
    public static void ShowConfig(ChatCommandContext ctx)
    {
        var sb = new System.Text.StringBuilder();

        string FormatStatus(bool? isEnabled) => isEnabled == true ? "enabled" : "disabled";

        string broadcastStatus = FormatStatus(Plugin.BroadcastMessageEnabled?.Value);
    //  string broadcastMessage = Regex.Replace(Plugin.BroadcastMessage?.Value, "<.*?>", string.Empty);
        string webhookStatus = FormatStatus(Plugin.WebhookEnabled?.Value);
    //  string webhookMessage = Plugin.WebhookMessage?.Value;
        string buffStatus = FormatStatus(Plugin.ClaimedBuffEnabled?.Value);
        string repairStatus = FormatStatus(Plugin.GearRepairOnClaimEnabled?.Value);

        sb.AppendLine($"<color=yellow>DailyQuest Configurations</color>");
        sb.AppendLine($"- Broadcast Status: {broadcastStatus}");
    //  sb.AppendLine($"- Broadcast Message: {broadcastMessage}");
        sb.AppendLine($"- Webhook Status: {webhookStatus}");
    //  sb.AppendLine($"- Webhook Message: {webhookMessage}");
        sb.AppendLine($"- Gear Repair: {repairStatus}");
        sb.AppendLine($"- Claimed Buff: {buffStatus}");

        ctx.Reply(sb.ToString().TrimEnd());
    }
}
