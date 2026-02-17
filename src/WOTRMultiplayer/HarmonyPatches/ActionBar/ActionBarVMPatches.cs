using HarmonyLib;
using Kingmaker.UI.MVVM._VM.ActionBar;
using Kingmaker.UI.UnitSettings;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Entities.ActionBar;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.Equipment;

namespace WOTRMultiplayer.HarmonyPatches.ActionBar
{
    [HarmonyPatch]
    public class ActionBarVMPatches
    {
        [HarmonyPatch(typeof(ActionBarVM), nameof(ActionBarVM.ClearSlot))]
        [HarmonyPrefix]
        public static void ActionBarVM_ClearSlot_Prefix(ActionBarSlotVM viewModel)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var slot = CreateActionBarSlot(viewModel);
            if (slot == null)
            {
                return;
            }

            if (slot.Index == -1)
            {
                Main.GetLogger<ActionBarVMPatches>().LogInformation("Skipping action bar slot clear due to bad index. SlotIndex={SlotIndex}", viewModel.Index);
                return;
            }

            Main.Multiplayer.OnClearActionBarSlot(slot);
        }

        [HarmonyPatch(typeof(ActionBarVM), nameof(ActionBarVM.MoveSlot))]
        [HarmonyPrefix]
        public static void ActionBarVM_MoveSlot_Prefix(ActionBarSlotVM sourceSlotVM, ActionBarSlotVM targetSlotVM)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var sourceSlot = CreateActionBarSlot(sourceSlotVM);
            var targetSlot = CreateActionBarSlot(targetSlotVM);

            if (sourceSlot == null || targetSlot == null)
            {
                return;
            }

            Main.Multiplayer.OnMoveActionBarSlot(sourceSlot, targetSlot);
        }

        private static NetworkActionBarSlot CreateActionBarSlot(ActionBarSlotVM actionBarSlotVM)
        {
            var slot = new NetworkActionBarSlot
            {
                Index = actionBarSlotVM.Index
            };

            switch (actionBarSlotVM.MechanicActionBarSlot)
            {
                case MechanicActionBarSlotActivableAbility activatableAbility:
                    slot.ActivatableAbility = new NetworkActivatableAbility
                    {
                        Id = activatableAbility.ActivatableAbility.UniqueId,
                        Name = activatableAbility.ActivatableAbility.NameForAcronym,
                        CasterId = activatableAbility.Unit.UniqueId,
                        IsActive = activatableAbility.ActivatableAbility.IsActive,
                        BlueprintId = activatableAbility.ActivatableAbility.Blueprint.AssetGuid.ToString()
                    };
                    slot.UnitId = slot.ActivatableAbility.CasterId;
                    break;
                case MechanicActionBarSlotAbility ability:
                    slot.Ability = Main.Mapper.Map<NetworkAbility>(ability.Ability);
                    slot.UnitId = ability.Unit.UniqueId;
                    break;
                case MechanicActionBarSlotSpontaneusConvertedSpell convertedSpell:
                    slot.Ability = Main.Mapper.Map<NetworkAbility>(convertedSpell.Spell);
                    slot.UnitId = convertedSpell.Unit.UniqueId;
                    break;
                case MechanicActionBarSlotSpell spell:
                    slot.Ability = Main.Mapper.Map<NetworkAbility>(spell.Spell);
                    slot.UnitId = spell.Unit.UniqueId;
                    break;
                case MechanicActionBarSlotItem item:
                    slot.Item = NetworkItem.FromItemEntity(item.Item);
                    slot.UnitId = item.Unit.UniqueId;
                    break;
                case MechanicActionBarSlotEmpty:
                    break;
                default:
                    Main.GetLogger<ActionBarVMPatches>().LogError("Unknown slot type. SlotIndex={SlotIndex}, SlotType={SlotType}", actionBarSlotVM.Index, actionBarSlotVM.MechanicActionBarSlot?.GetType().Name);
                    return null;
            }

            return slot;
        }
    }
}
