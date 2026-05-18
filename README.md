# DailyQuest

DailyQuest is a **server-side** V Rising mod featuring three rotating quest tiers, customizable quest rewards, optional gear repair, reward buffs, and Discord webhook notifications.

## What's New in v1.1.0
- **Refactoring & Optimization:** Completely reworked the backend systems.
  - **Automated Data Migration:** Restructured the configuration files. Your existing settings will be automatically migrated to the new `DailyQuest.cfg` file.
  - **Safe Backups:** Legacy configuration files (`quest_config.json` and `webhook_config.json`) are automatically backed up in `BepInEx\config\DailyQuest\OldVersionBackup`.
  - **Performance Improvements:** Fixed minor server lag/stutters that could occur when players killed multiple NPCs simultaneously.
- **General Configuration Update:** Centralized core settings (Webhooks, Broadcasts, Buffs, and Gear Repair) into `DailyQuest.cfg` for easier management.
- **New Reward Buff Feature:** Added a new feature that grants players a buff when claiming quest rewards. You can enable/disable this feature and customize the buff prefab in the configuration.

## Features
- Assigns **3 daily quests** to each player every day:
  - **Quest 1 (Easy)**
  - **Quest 2 (Medium)**
  - **Quest 3 (Hard)**
- Tracks kill progress automatically
- Stores daily quest progress per player
- Uses a simple JSON configuration file
- Includes admin commands to reload the configurations
- Lets players claim rewards with chat commands
- Supports **optional gear repair when claiming rewards**
  - Quest 1 reward: repairs **amulet** in the character's equipment slots (excluding Soul Shards)
  - Quest 2 reward: repairs **armor** in the character's equipment slots
  - Quest 3 reward: repairs **weapons** in the character's equipment slots
- Supports **optional Discord notifications when claiming rewards**
- Supports **optional reward buffs when claiming rewards**

## Requirements
1. [BepInEx 1.733.2](https://thunderstore.io/c/v-rising/p/BepInEx/BepInExPack_V_Rising/) 
2. [VampireCommandFramework 0.11.0](https://thunderstore.io/c/v-rising/p/deca/VampireCommandFramework/)

## Installation
1. Install the required dependencies.
2. Place `DailyQuest.dll` into your server's BepInEx plugins folder.
3. Start the server once to generate the config files.
4. Edit the config files.
5. Restart the server or reload the quest configuration.

## How It Works
- Each player is assigned one Easy, one Medium, and one Hard quest for the day.
- Progress is tracked automatically when the player kills matching configured targets.
- Players can check progress at any time with `.quest daily`.
- Completed rewards can be claimed with `.quest reward`.
- Quest assignments refresh daily.

## Commands

### Player Commands
- `.quest daily` 
   - Show your current daily quests and progress.
   - Shortcut: *.quest d*

- `.quest reward` 
  - Claim all completed daily quest rewards.
  - Shortcut: *.quest rw*

### Admin Commands
- `.quest info <player>` 
  - Show daily quest status for a specific player.
  - Shortcut: *.quest i <player>*

- `.quest debuff <player>`
  - Force remove the daily quest buff from a player.
  - Shortcut: *.quest db <player>*

- `.quest testwebhook`
  - Send a test message to Discord.
  - Shortcut: *.quest tw*

- `.quest config`
  - Display the DailyQuest configuration.
  - Shortcut: *.quest cfg*

- `.quest reload`
  - Reload both quest_config.json and DailyQuest.cfg.
  - Shortcut: *.quest rl*

## Config Files

After the first server start, the following files will be created:
- `BepInEx/config/DailyQuest.cfg`
- `BepInEx/config/DailyQuest/quest_config.json`
- `BepInEx/config/DailyQuest/quest_player.json`

### DailyQuest.cfg
This file contains the core toggle settings for the mod, including enabling/disabling Broadcasts, Webhooks, Buffs, and Gear Repair, as well as customizable messages.

### quest_config.json
This file contains the main DailyQuest settings and quest definitions.
- `ID`: the unique ID of the quest.
- `Name`: the name of the quest.
- `Difficulty`: the quest difficulty (`easy` = Quest 1, `medium` = Quest 2, `hard` = Quest 3).
- `TargetPrefabs`: the target prefab IDs for the quest.
- `RequiredKills`: the number of kills required to complete the quest.
- `Reward.Prefab`: the reward prefab ID.
- `Reward.Name`: the display name of the reward.
- `Reward.Amount`: the reward amount.

### quest_player.json
This file stores each player's daily quest assignments and progress.
- Do not edit `quest_player.json` unless you know exactly what you are doing.

## Credits
- [KindredCommands](https://thunderstore.io/c/v-rising/p/odjit/KindredCommands/) by **odjit** for the original code that inspired this mod.
- Special thanks to [**odjit**](https://thunderstore.io/c/v-rising/p/odjit/) and [**zfolmt**](https://thunderstore.io/c/v-rising/p/zfolmt/) for their original code and assistance with this mod.
- [**V Rising Modding Community**](https://discord.com/invite/QG2FmueAG9)

## License
This project is licensed under the AGPL-3.0 license.

## Notes
> - This mod was first developed for my own server and originally built around [KindredCommands](https://thunderstore.io/c/v-rising/p/odjit/KindredCommands/). Special thanks to **odjit** for the amazing mod and inspiration behind this project.
> - If you have any problems or run into bugs, please report them to me in the [V Rising Modding Community](https://discord.com/invite/QG2FmueAG9).
> **Del** (delta_663)
