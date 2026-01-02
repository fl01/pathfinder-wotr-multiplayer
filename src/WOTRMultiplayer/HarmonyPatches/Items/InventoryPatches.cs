using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Items;
using Kingmaker.Items.Slots;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.Utility;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.Equipment;
using WOTRMultiplayer.Entities.Items;
using static Kingmaker.Blueprints.Items.Components.ItemPolymorph;

namespace WOTRMultiplayer.HarmonyPatches.Items
{
    [HarmonyPatch]
    public class InventoryPatches
    {
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
            var extraCall = AccessTools.Method(typeof(InventoryPatches), nameof(InventoryPatches.OnUseItemFromInventory));
            var lookFor = AccessTools.Constructor(typeof(RuleCastSpell), [typeof(Ability), typeof(TargetWrapper)]);
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Is(OpCodes.Newobj, lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<InventoryPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
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
            Main.GetLogger<InventoryPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(ItemPolymorphPart), nameof(ItemPolymorphPart.CreateAndEquipPolymorphInSlot))]
        [HarmonyPrefix]
        public static bool ItemPolymorphPart_CreateAndEquipPolymorphInSlot_Prefix(ItemEntity item, UnitEntityData equipTarget, ItemSlot itemSlot)
        {
            if (!Main.Multiplayer.IsActive || item == null || !item.CanBeEquippedBy(equipTarget))
            {
                return true;
            }

            var slotPosition = Main.Multiplayer.GetEquipmentSlotPosition(itemSlot);
            var unitId = equipTarget.UniqueId;
            var polymorphicItem = new NetworkPolymorphicItem
            {
                Item = NetworkItem.FromItemEntity(item),
                Position = slotPosition,
                UnitId = unitId
            };

            var shouldContinue = Main.Multiplayer.OnCreateAndEquipPolymorphInSlot(polymorphicItem);
            return shouldContinue;
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
    }
}
