using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Items;
using Kingmaker.UI.MVVM._VM.Loot;
using Kingmaker.UI.MVVM._VM.Slots;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Loot;

namespace WOTRMultiplayer.HarmonyPatches.Loot
{
    [HarmonyPatch]
    public class LootPatches
    {
        [HarmonyPatch(typeof(LootContextVM), nameof(LootContextVM.HandleLootInterraction))]
        [HarmonyPrefix]
        public static bool LootContextVM_HandleLootInterraction_Prefix(LootContextVM __instance, UnitEntityData unit)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canLoot = Main.Multiplayer.CanLootUnit(unit.UniqueId);
            if (canLoot)
            {
                return true;
            }

            return false;
        }

        [HarmonyPatch(typeof(LootObjectVM), nameof(LootObjectVM.TransferAllItems))]
        [HarmonyPrefix]
        public static void LootObjectVM_TransferAllItems_Prefix(LootObjectVM __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var lootOwner = __instance.ItemsCollection.OwnerRef.Entity;
            var items = __instance.ItemsCollection.Where(i => i.IsLootable && !i.NeedSkinningForCollect).ToList();
            var container = CreateLootContainer(lootOwner, items);

            Main.Multiplayer.OnLootContainer(container);
        }

        [HarmonyPatch(typeof(SlotsGroupVM<ItemSlotVM>), nameof(SlotsGroupVM<ItemSlotVM>.Collect))]
        [HarmonyPrefix]
        public static void SlotsGroupVM_Collect_Prefix(SlotsGroupVM<ItemSlotVM> __instance, ItemSlotVM itemSlotVM)
        {
            if (!Main.Multiplayer.IsActive || itemSlotVM == null || !itemSlotVM.HasItem || itemSlotVM.NeedSkinningToCollect && (!itemSlotVM.IsSkinned.Value || !itemSlotVM.SkinningResult))
            {
                return;
            }

            var lootOwner = itemSlotVM.ParentCollection.OwnerRef.Entity;
            var container = CreateLootContainer(lootOwner, [itemSlotVM.ItemEntity]);

            Main.Multiplayer.OnLootContainer(container);
        }

        private static NetworkLootContainer CreateLootContainer(EntityDataBase lootOwner, List<ItemEntity> itemEntities)
        {
            var container = new NetworkLootContainer
            {
                Id = lootOwner.UniqueId,
                Position = new NetworkVector3(lootOwner.Position.x, lootOwner.Position.y, lootOwner.Position.z),
                IsMapObject = lootOwner is MapObjectEntityData,
                Items = [.. itemEntities.Select(x => new NetworkLootItem
                {
                    UniqueId = x.UniqueId,
                    BlueprintId = x.Blueprint.AssetGuid.ToString(),
                    Name = x.NameForAcronym,
                    Count = x.Count,
                    Cost = x.Cost,
                    EnchantmentValue = x.EnchantmentValue,
                    EnchantmentsCount = x.Enchantments.Count,
                    FirstEnchantmentName = x.Enchantments.FirstOrDefault()?.NameForAcronym
                })]
            };

            return container;
        }
    }
}
