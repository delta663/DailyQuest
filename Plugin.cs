using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using VampireCommandFramework;
using System.Collections;
using DailyQuest.Services;

namespace DailyQuest;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("gg.deca.VampireCommandFramework")]
public class Plugin : BasePlugin
{
    internal static Harmony Harmony;
    internal static ManualLogSource PluginLog;
    public static ManualLogSource LogInstance { get; private set; }

    public static ConfigFile PluginConfig;
    public static ConfigEntry<bool> GearRepairOnClaimEnabled;
    public static ConfigEntry<bool> BroadcastMessageEnabled;
    public static ConfigEntry<string> BroadcastMessage;
    public static ConfigEntry<bool> WebhookEnabled;
    public static ConfigEntry<string> WebhookUrl;
    public static ConfigEntry<string> WebhookMessage;
    public static ConfigEntry<bool> ClaimedBuffEnabled;
    public static ConfigEntry<int> ClaimedBuffPrefab;

    public override void Load()
    {
        if (Application.productName != "VRisingServer") return;

        PluginLog = Log;
        LogInstance = Log;
        PluginConfig = Config;

        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} version {MyPluginInfo.PLUGIN_VERSION} is loaded!");

        GearRepairOnClaimEnabled = Config.Bind("Repair", "GearRepairOnClaim", false, "Enable gear repair when rewards are claimed.");
        BroadcastMessageEnabled = Config.Bind("Broadcast", "BroadcastEnabled", true, "Enable in-game broadcast message when rewards are claimed.");
        BroadcastMessage = Config.Bind("Broadcast", "BroadcastMessage", "<color=white>#player#</color> completed and claimed rewards for: #quest#. Use <color=green>.quest daily</color> to check your quests.", "Format of the in-game broadcast message.");
        WebhookEnabled = Config.Bind("Webhook", "WebhookEnabled", false, "Enable Discord webhook message when rewards are claimed.");
        WebhookUrl = Config.Bind("Webhook", "WebhookUrl", "", "Webhook URL. Example: https://discord.com/api/webhooks/123456789012345678/aBc1234d5Efg6H78ij9k0-L1m2nO3pq4RstU5v6w78Xyz9");
        WebhookMessage = Config.Bind("Webhook", "WebhookMessage", "**[Daily quest]** - **#player#** has completed and claimed the rewards for: #quest#", "Format of the Discord webhook message.");
        ClaimedBuffEnabled = Config.Bind("Buff", "ClaimedBuffEnabled", false, "Enable giving a buff when rewards are claimed.");
        ClaimedBuffPrefab = Config.Bind("Buff", "ClaimedBuffPrefab", -463147620, "PrefabGUID of the buff to apply.");

        try
        {
            QuestService.EnsureFilesExist();
            MigrationService.RunAllMigrations();         
            
            Log.LogInfo("DailyQuest config files ensured.");
        }
        catch (System.Exception e)
        {
            Log.LogError($"Failed to create DailyQuest files: {e}");
        }

        Harmony = new Harmony("dailyquest");
        Harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());

        CommandRegistry.RegisterAll();
    }

    public override bool Unload()
    {
        CommandRegistry.UnregisterAssembly();
        Harmony?.UnpatchSelf();
        
        SaveThrottle.Stop();
        return true;
    }
}
