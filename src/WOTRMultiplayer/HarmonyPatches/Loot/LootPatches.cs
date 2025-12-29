using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Items;
using Kingmaker.Localization;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.UI.MVVM._PCView.Loot;
using Kingmaker.UI.MVVM._VM.Loot;
using Kingmaker.UI.MVVM._VM.Slots;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.Utility;
using Kingmaker.View.MapObjects;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.Equipment;
using WOTRMultiplayer.Entities.Loot;

namespace WOTRMultiplayer.HarmonyPatches.Loot
{
    [HarmonyPatch]
    public class LootPatches
    {
        [HarmonyPatch(typeof(LootCollectorPCView), nameof(LootCollectorPCView.UpdateButtons))]
        [HarmonyPostfix]
        public static void LootCollectorPCView_UpdateButtons_Postfix(LootCollectorPCView __instance)
        {
            if (!Main.Multiplayer.IsActive || __instance.ViewModel.Mode != LootContextVM.LootWindowMode.ZoneExit)
            {
                return;
            }

            Main.Multiplayer.OnZoneLootCollectorButtonsUpdated();
        }

        [HarmonyPatch(typeof(LootPCView), nameof(LootPCView.BindViewImplementation))]
        [HarmonyPostfix]
        public static void LootPCView_BindViewImplementation_Postfix(LootPCView __instance)
        {
            if (!Main.Multiplayer.IsActive || __instance.ViewModel.Mode != LootContextVM.LootWindowMode.ZoneExit)
            {
                return;
            }

            Main.Multiplayer.OnZoneLootShown();
        }

        [HarmonyPatch(typeof(LootVM), nameof(LootVM.Close))]
        [HarmonyPrefix]
        public static void LootVM_Close_Prefix(LootVM __instance)
        {
            if (!Main.Multiplayer.IsActive || __instance.Mode != LootContextVM.LootWindowMode.ZoneExit)
            {
                return;
            }

            Main.Multiplayer.OnZoneLootClosed();
        }

        [HarmonyPatch(typeof(LootVM), nameof(LootVM.SwitchRemoveUncollectedLoot))]
        [HarmonyPostfix]
        public static void LootVM_SwitchRemoveUncollectedLoot_Postfix(LootVM __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var removeUncollectedLoot = __instance.RemoveUncollectedLoot.Value;
            Main.Multiplayer.OnZoneLootRemoveToggleChanged(removeUncollectedLoot);
        }

        [HarmonyPatch(typeof(LootVM), nameof(LootVM.LeaveZone))]
        [HarmonyPrefix]
        public static void LootVM_LeaveZone_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnZoneLootCompleted();
        }

        [HarmonyPatch(typeof(LootContextVM), nameof(LootContextVM.HandleLootInterraction))]
        [HarmonyPrefix]
        public static bool LootContextVM_HandleLootInterraction_Prefix(UnitEntityData unit, LootContainerType containerType)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var showLootingWindow = containerType == LootContainerType.PlayerChest || Main.Multiplayer.IsControlledByLocalPlayer(unit.UniqueId);
            return showLootingWindow;
        }

        [HarmonyPatch(typeof(LootObjectVM), nameof(LootObjectVM.UseSkinning))]
        [HarmonyPrefix]
        public static void LootObjectVM_UseSkinning_Prefix(LootObjectVM __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var entity = CreateLootableEntity(__instance.ItemsCollection);

            Main.Multiplayer.OnSkinLootContainer(entity);
        }

        [HarmonyPatch(typeof(LootObjectVM), nameof(LootObjectVM.TransferAllItems))]
        [HarmonyPrefix]
        public static void LootObjectVM_TransferAllItems_Prefix(LootObjectVM __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var items = __instance.ItemsCollection.Where(i => i.IsLootable && !i.NeedSkinningForCollect).ToList();
            var transfer = CreateItemsTransfer(__instance.ItemsCollection, items, null);

            Main.Multiplayer.OnTransferInventoryItems(transfer);
        }

        /// <summary>
        /// This one covers clicks on container items
        /// </summary>
        /// <param name="itemSlotVM"></param>
        [HarmonyPatch(typeof(SlotsGroupVM<ItemSlotVM>), nameof(SlotsGroupVM<ItemSlotVM>.Collect))]
        [HarmonyPrefix]
        public static void SlotsGroupVM_Collect_Prefix(ItemSlotVM itemSlotVM)
        {
            if (!Main.Multiplayer.IsActive || itemSlotVM == null || !itemSlotVM.HasItem || itemSlotVM.NeedSkinningToCollect && (!itemSlotVM.IsSkinned.Value || !itemSlotVM.SkinningResult))
            {
                return;
            }

            var transfer = CreateItemsTransfer(itemSlotVM.ParentCollection, [itemSlotVM.ItemEntity], null);

            Main.Multiplayer.OnTransferInventoryItems(transfer);
        }

        /// <summary>
        /// This one covers drag&drops + inventory -> stash/container transfers
        /// </summary>
        /// <param name="itemSlotVM"></param>
        /// <param name="count"></param>
        /// <param name="targetCollection"></param>
        [HarmonyPatch(typeof(SlotsGroupVM<ItemSlotVM>), nameof(SlotsGroupVM<ItemSlotVM>.TransferCount))]
        [HarmonyPrefix]
        public static void SlotsGroupVM_TransferCount_Prefix(ItemSlotVM itemSlotVM, int count, ISlotsGroupVM targetCollection)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            if (targetCollection.MechanicCollection.OwnerRef.Entity == null)
            {
                var errorMessage = "Unable to move items into disposed container. Denying action since those items will be deleted once UI is closed";
                Main.GetLogger<LootPatches>().LogWarning(errorMessage);
                var messageKey = new LocalizedString { Key = WellKnownKeys.GameNotifications.Looting.DisposedLootbagBugWarning.Key };
                EventBus.RaiseEvent<IWarningNotificationUIHandler>(x => x.HandleWarning(messageKey, true), true);
                throw new InvalidOperationException(errorMessage);
            }

            var item = NetworkItem.FromItemEntity(itemSlotVM.ItemEntity);
            item.Count = count;
            var transfer = CreateItemsTransfer(itemSlotVM.ItemEntity.Collection, [item], targetCollection.MechanicCollection);

            Main.Multiplayer.OnTransferInventoryItems(transfer);
        }

        [HarmonyPatch(typeof(ItemsCollection), nameof(ItemsCollection.DropItem))]
        [HarmonyPrefix]
        public static void ItemsCollection_DropItem_Prefix(ItemsCollection __instance, ItemEntity item)
        {
            if (!Main.Multiplayer.IsActive || __instance != item.Collection || __instance.OwnerRef.Entity is not Kingmaker.Player player)
            {
                return;
            }

            var dropItem = new NetworkDropItem
            {
                OwnerEntityId = player.MainCharacter.UniqueId,
                Item = NetworkItem.FromItemEntity(item)
            };

            Main.Multiplayer.OnDropItem(dropItem);
        }

        [HarmonyPatch(typeof(ItemEntity), nameof(ItemEntity.TryUseFromInventory))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> ItemEntity_TryUseFromInventory_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var extraCall = AccessTools.Method(typeof(LootPatches), nameof(LootPatches.OnUseItemFromInventory));
            var lookFor = AccessTools.Constructor(typeof(RuleCastSpell), [typeof(Ability), typeof(TargetWrapper)]);
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Is(OpCodes.Newobj, lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<LootPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            match = match.Advance(-2);
            var labels = match.Instruction.ExtractLabels();

            var newInstructions = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldarg_0).WithLabels(labels),
                new (OpCodes.Ldarg_1),
                new (OpCodes.Ldarg_2),
                new (OpCodes.Call, extraCall),
            };

            match = match.Insert(newInstructions);
            Main.GetLogger<LootPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        public static void OnUseItemFromInventory(ItemEntity item, UnitEntityData user, TargetWrapper target)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var useInventoryItem = new NetworkUseInventoryItem
            {
                UserUnitId = user.UniqueId,
                SlotPosition = Main.Multiplayer.GetEquipmentSlotPosition(item.HoldingSlot),
                Target = new NetworkTargetWrapper(new NetworkVector3(target.m_Point.x, target.m_Point.y, target.m_Point.z), target.m_Orientation, target.Unit?.UniqueId),
                Item = NetworkItem.FromItemEntity(item)
            };

            Main.Multiplayer.OnUseInventoryItem(useInventoryItem);
        }

        private static NetworkItemsTransfer CreateItemsTransfer(ItemsCollection source, List<ItemEntity> items, ItemsCollection destination)
        {
            return CreateItemsTransfer(source, [.. items.Select(NetworkItem.FromItemEntity)], destination);
        }

        private static NetworkItemsTransfer CreateItemsTransfer(ItemsCollection source, List<NetworkItem> items, ItemsCollection destination)
        {
            var transferItems = new NetworkItemsTransfer
            {
                Source = CreateLootableEntity(source),
                Items = items,
                Destination = CreateLootableEntity(destination),
            };

            return transferItems;
        }

        private static NetworkLootableEntity CreateLootableEntity(ItemsCollection collection)
        {
            if (collection == null)
            {
                return null;
            }

            var owner = collection.OwnerRef.Entity;

            var entity = new NetworkLootableEntity
            {
                Id = owner.UniqueId,
                Position = new NetworkVector3(owner.Position.x, owner.Position.y, owner.Position.z),
                Type = GetNetworkLootableEntityType(owner, collection)
            };

            return entity;
        }

        private static NetworkLootableEntityType GetNetworkLootableEntityType(EntityDataBase owner, ItemsCollection collection)
        {
            // TODO: Support other stash types: Player.SharedStashType.MEMORIES, Player.SharedStashType.BESMARITES
            // there is no info where those stashes are used, seems like Act3+
            var type = owner switch
            {
                UnitEntityData => NetworkLootableEntityType.Unit,

                Kingmaker.Player when collection.IsPlayerInventory => NetworkLootableEntityType.Player,
                Kingmaker.Player => NetworkLootableEntityType.MainStash,

                _ => NetworkLootableEntityType.MapObject
            };

            return type;
        }
    }
}
