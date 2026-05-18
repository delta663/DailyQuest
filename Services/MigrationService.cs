using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DailyQuest.Models;

namespace DailyQuest.Services;

internal static class MigrationService
{
    private static readonly string CONFIG_DIR = Path.Combine(BepInEx.Paths.ConfigPath, MyPluginInfo.PLUGIN_NAME);
    private static readonly string QUEST_CONFIG_FILE = Path.Combine(CONFIG_DIR, "quest_config.json");
    private static readonly string WEBHOOK_CONFIG_FILE = Path.Combine(CONFIG_DIR, "webhook_config.json");

    private static readonly string DATE_TIME = DateTime.Now.ToString("yyyyMMdd_HHmmss");
    private static readonly string BACKUP_DIR = Path.Combine(CONFIG_DIR, "OldVersionBackup");
    private static readonly string QUEST_CONFIG_BACKUP_FILE = Path.Combine(BACKUP_DIR, $"quest_config_backup_{DATE_TIME}.json");
    private static readonly string WEBHOOK_CONFIG_BACKUP_FILE = Path.Combine(BACKUP_DIR, $"webhook_config_backup_{DATE_TIME}.json");
    private static readonly string README_FILE = Path.Combine(BACKUP_DIR, "readme.txt");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true
    };

    public static void RunAllMigrations()
    {
        bool questMigrated = MigrateQuestConfig();
        bool webhookMigrated = MigrateWebhookConfig();

        if (questMigrated || webhookMigrated)
        {
            CreateMigrationInfoFile();
        }
    }

    private static bool MigrateQuestConfig()
    {
        if (!File.Exists(QUEST_CONFIG_FILE)) return false;

        try
        {
            string json = File.ReadAllText(QUEST_CONFIG_FILE);

            if (json.Contains("\"GearRepairOnClaim\":") || json.Contains("\"Quests\":"))
            {
                Core.Log.LogWarning("[MigrationService] LEGACY QUEST FORMAT DETECTED! STARTING MIGRATION...");

                Directory.CreateDirectory(BACKUP_DIR);
                File.Copy(QUEST_CONFIG_FILE, QUEST_CONFIG_BACKUP_FILE, true);
                Core.Log.LogInfo($"[MigrationService] Step 1. Quest Backup created at: {QUEST_CONFIG_BACKUP_FILE}");

                var legacyConfig = JsonSerializer.Deserialize<QuestConfig>(json, JsonOpts) ?? new QuestConfig();

                if (Plugin.GearRepairOnClaimEnabled != null)
                {
                    Plugin.GearRepairOnClaimEnabled.Value = legacyConfig.RepairOnClaim;
                    Plugin.PluginConfig.Save();
                    Core.Log.LogInfo("[MigrationService] Step 2. GearRepairOnClaim config moved to DailyQuest.cfg");
                }

                var newJsonBytes = JsonSerializer.SerializeToUtf8Bytes(legacyConfig.Quests ?? new List<QuestDef>(), JsonOpts);
                File.WriteAllBytes(QUEST_CONFIG_FILE, newJsonBytes);
                Core.Log.LogInfo("[MigrationService] Step 3. Quest file rewritten to NEW format successfully!");
                
                return true;
            }
        }
        catch (Exception e)
        {
            Core.Log.LogError($"[MigrationService] Quest Migration Error: {e.Message}");
        }
        return false;
    }

    private static bool MigrateWebhookConfig()
    {
        if (!File.Exists(WEBHOOK_CONFIG_FILE)) return false;

        try
        {
            Core.Log.LogWarning("[MigrationService] LEGACY WEBHOOK FORMAT DETECTED! STARTING MIGRATION...");
            
            Directory.CreateDirectory(BACKUP_DIR);
            File.Copy(WEBHOOK_CONFIG_FILE, WEBHOOK_CONFIG_BACKUP_FILE, true);
            Core.Log.LogInfo($"[MigrationService] Step 1. Webhook Backup created at: {WEBHOOK_CONFIG_BACKUP_FILE}");

            var json = File.ReadAllText(WEBHOOK_CONFIG_FILE);
            var oldConfig = JsonSerializer.Deserialize<LegacyWebhookConfig>(json, JsonOpts);

            if (oldConfig != null)
            {
                Plugin.WebhookEnabled.Value = oldConfig.Enabled;
                Plugin.WebhookUrl.Value = oldConfig.WebhookUrl ?? "";
                Plugin.PluginConfig.Save(); 
                Core.Log.LogInfo("[MigrationService] Step 2. Webhook settings moved to DailyQuest.cfg");
            }
            
            File.Delete(WEBHOOK_CONFIG_FILE);
            Core.Log.LogInfo("[MigrationService] Step 3. Old webhook config file deleted successfully!");
            
            return true;
        }
        catch (Exception e)
        {
            Core.Log.LogError($"[MigrationService] Webhook Migration Error: {e.Message}");
        }
        return false;
    }

    private static void CreateMigrationInfoFile()
    {
        try
        {
            Directory.CreateDirectory(BACKUP_DIR);

            string content = $@"DailyQuest Migration Info Created at {DateTime.Now:yyyy-MM-dd HH:mm:ss}
- 'quest_config_backup_xxxxxxxx_xxxxxx.json' and 'webhook_config_xxxxxxxx_xxxxxx.json' in {BACKUP_DIR} were created to safely back up your configurations from version 1.0.4 or older.
- As of version 1.1.0, the 'GearRepairOnClaim' setting and all webhook configurations have been consolidated into 'BepInEx/config/DailyQuest.cfg' for easier management.
- We have restructured 'quest_config.json' to contain only the quest list. All your existing quest setups will continue to function normally.
- You can open the 'BepInEx/config/DailyQuest.cfg' to review the new configuration format.
- If any settings in 'DailyQuest.cfg' are missing or incorrect, you can manually restore your original configurations from the backup files mentioned above.
";

            File.WriteAllText(README_FILE, content);
            Core.Log.LogInfo($"[MigrationService] Migration info file created at: {README_FILE}");
        }
        catch (Exception e)
        {
            Core.Log.LogWarning($"[MigrationService] Could not create info file: {e.Message}");
        }
    }

    private sealed class LegacyWebhookConfig
    {
        public bool Enabled { get; set; }
        public string WebhookUrl { get; set; }
    }
}