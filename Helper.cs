using System.Collections.Generic;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using ProjectM.Scripting;
using ProjectM.Shared;
using System;
using System.Threading.Tasks;
using DailyQuest.Services;
using DailyQuest.Models;
using Unity.Collections;

namespace DailyQuest;

internal static partial class Helper
{
    public static PrefabGUID GetPrefabGUID(Entity entity)
    {
        var entityManager = Core.EntityManager;
        try
        {
            return entityManager.GetComponentData<PrefabGUID>(entity);
        }
        catch
        {
            return new PrefabGUID(0);
        }
    }

    public static NativeArray<Entity> GetEntitiesByComponentType<T>()
    {
        var query = Core.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
        var entities = query.ToEntityArray(Allocator.Temp);
        query.Dispose();
        return entities;
    }

    public static NativeArray<Entity> GetEntitiesByComponentTypes<T1, T2>()
    {
        var query = Core.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<T1>(), ComponentType.ReadOnly<T2>());
        var entities = query.ToEntityArray(Allocator.Temp);
        query.Dispose();
        return entities;
    }

    public static Entity AddItemToInventory(Entity recipient, PrefabGUID guid, int amount)
    {
        try
        {
            var serverGameManager = Core.Server.GetExistingSystemManaged<ServerScriptMapper>()._ServerGameManager;
            var inventoryResponse = serverGameManager.TryAddInventoryItem(recipient, guid, amount);
            return inventoryResponse.NewEntity;
        }
        catch (System.Exception e)
        {
            Core.LogException(e);
        }
        return new Entity();
    }

    private static readonly HashSet<int> SoulShards = new()
    {
        666638454,
        -1581189572,
        -1260254082,
        -21943750,
        1286615355
    };

    private static void HandleEquipment(Entity itemEntity, bool repair)
    {
        if (itemEntity == Entity.Null || !itemEntity.Has<Durability>()) return;

        var durability = itemEntity.Read<Durability>();
        durability.Value = repair ? durability.MaxDurability : 0;
        itemEntity.Write(durability);
    }

    public static void RepairArmor(Entity character, bool repair = true)
    {
        try
        {
            if (!character.Has<Equipment>()) return;

            var equipment = character.Read<Equipment>();
            HandleEquipment(equipment.ArmorChestSlot.SlotEntity.GetEntityOnServer(), repair);
            HandleEquipment(equipment.ArmorGlovesSlot.SlotEntity.GetEntityOnServer(), repair);
            HandleEquipment(equipment.ArmorLegsSlot.SlotEntity.GetEntityOnServer(), repair);
            HandleEquipment(equipment.ArmorFootgearSlot.SlotEntity.GetEntityOnServer(), repair);
        }
        catch (Exception e)
        {
            Core.LogException(e);
        }
    }

    public static void RepairAmulet(Entity character, bool repair = true)
    {
        try
        {
            if (!character.Has<Equipment>()) return;

            var equipment = character.Read<Equipment>();
            var grimoire = equipment.GrimoireSlot.SlotEntity.GetEntityOnServer();

            if (grimoire == Entity.Null || !grimoire.Has<PrefabGUID>()) return;

            var prefab = grimoire.Read<PrefabGUID>();
            if (SoulShards.Contains(prefab.GuidHash)) return;

            HandleEquipment(grimoire, repair);
        }
        catch (Exception e)
        {
            Core.LogException(e);
        }
    }

    public static void RepairWeapon(Entity character, bool repair = true)
    {
        try
        {
            if (!InventoryUtilities.TryGetInventoryEntity(Core.EntityManager, character, out var inventory)) return;

            var mapper = Core.Server.GetExistingSystemManaged<ServerScriptMapper>();
            if (mapper == null) return;

            var serverGameManager = mapper._ServerGameManager;
            if (!serverGameManager.TryGetBuffer<InventoryBuffer>(inventory, out var buffer)) return;

            for (int i = 0; i < 8 && i < buffer.Length; i++)
            {
                var entry = buffer[i];
                var itemEntity = entry.ItemEntity.GetEntityOnServer();

                if (itemEntity == Entity.Null) continue;
                if (!itemEntity.Has<Durability>()) continue;
                if (!itemEntity.Has<EquippableData>()) continue;

                var equipData = itemEntity.Read<EquippableData>();
                if (equipData.EquipmentType != EquipmentType.Weapon) continue;

                HandleEquipment(itemEntity, repair);
            }
        }
        catch (Exception e)
        {
            Core.LogException(e);
        }
    }

    public static void TrySendBroadcastMessage(string playerName, List<string> claimedNames)
    {
        if (Plugin.BroadcastMessageEnabled != null && !Plugin.BroadcastMessageEnabled.Value) return;

        try
        {
            string list = string.Join(", ", claimedNames);
            string format = Plugin.BroadcastMessage?.Value ?? "";
            string msg = format.Replace("#player#", playerName).Replace("#quest#", list);

            var fs = new FixedString512Bytes(msg);
            ServerChatUtils.SendSystemMessageToAllClients(Core.EntityManager, ref fs);
        }
        catch (Exception e)
        {
            Core.LogException(e);
        }
    }

    public static void TrySendWebhookMessage(string playerName, List<string> claimedNames)
    {
        if (Plugin.WebhookEnabled != null && !Plugin.WebhookEnabled.Value) return;
        
        try
        {
            string list = string.Join(", ", claimedNames);
            string format = Plugin.WebhookMessage?.Value ?? "";
            string message = format.Replace("#player#", playerName).Replace("#quest#", list);
            
            _ = TrySendWebhookAsync(message);
        }
        catch (Exception e)
        {
            Core.LogException(e);
        }
    }

    public static async Task TrySendWebhookAsync(string message)
    {
        try
        {
            var (ok, error) = await WebhookService.SendAsync(message).ConfigureAwait(false);
            if (!ok && !string.IsNullOrWhiteSpace(error) &&
                !string.Equals(error, "Webhook is disabled.", StringComparison.Ordinal) &&
                !string.Equals(error, "Webhook URL is empty.", StringComparison.Ordinal))
            {
                Core.Log.LogWarning($"[Webhook] {error}");
            }
        }
        catch (Exception e)
        {
            Core.LogException(e);
        }
    }

    public static void TryAddDailyQuestBuff(Entity userEntity, Entity characterEntity, PlayerQuestState st)
    {
        if (Plugin.ClaimedBuffEnabled == null || !Plugin.ClaimedBuffEnabled.Value) return;

        int newBuffId = Plugin.ClaimedBuffPrefab?.Value ?? 0;
        if (newBuffId == 0) return;

        try
        {
            if (st.ClaimedBuffPrefab != 0 && st.ClaimedBuffPrefab != newBuffId)
            {
                Buffs.RemoveBuff(characterEntity, new PrefabGUID(st.ClaimedBuffPrefab));
            }

            Buffs.AddBuff(userEntity, characterEntity, new PrefabGUID(newBuffId), duration: -1);
                        
            st.ClaimedBuffPrefab = newBuffId; 
        }
        catch (Exception e)
        {
            Core.LogException(e);
        }
    }

    public static bool TryRemoveDailyQuestBuff(Entity characterEntity, PlayerQuestState st)
    {
        bool changed = false;

        if (characterEntity == default || characterEntity == Entity.Null) 
            return false;

        try
        {
            if (st.ClaimedBuffPrefab != 0)
            {
                Buffs.RemoveBuff(characterEntity, new PrefabGUID(st.ClaimedBuffPrefab));
                st.ClaimedBuffPrefab = 0;
                changed = true;
            }

            int currentConfigBuffId = Plugin.ClaimedBuffPrefab?.Value ?? 0;
            if (currentConfigBuffId != 0)
            {
                Buffs.RemoveBuff(characterEntity, new PrefabGUID(currentConfigBuffId));
            }
        }
        catch (Exception e)
        {
            Core.LogException(e);
        }

        return changed;
    }
}
