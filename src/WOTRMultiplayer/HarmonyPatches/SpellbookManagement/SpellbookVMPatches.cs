using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.ServiceWindows.Spellbook;
using Kingmaker.UI.MVVM._VM.ServiceWindows.Spellbook;
using Kingmaker.UI.MVVM._VM.ServiceWindows.Spellbook.KnownSpells;
using Kingmaker.UI.MVVM._VM.ServiceWindows.Spellbook.MemorizingPanel;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.Spells;

namespace WOTRMultiplayer.HarmonyPatches.SpellbookManagement
{
    [HarmonyPatch]
    public class SpellbookVMPatches
    {
        [HarmonyPatch(typeof(SpellbookPCView), nameof(SpellbookPCView.BindViewImplementation))]
        [HarmonyPostfix]
        public static void SpellbookPCView_BindViewImplementation_Postfix(SpellbookPCView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var unitId = __instance.ViewModel.UnitDescriptor?.Value?.Unit?.UniqueId;
            if (__instance.m_KnownSpellsView.m_KnownSpellView != null)
            {
                __instance.m_KnownSpellsView.m_KnownSpellView.m_RemoveButton.Interactable = Main.Multiplayer.IsControlledByLocalPlayer(unitId);
            }
        }

        [HarmonyPatch(typeof(SpellbookVM), nameof(SpellbookVM.RemoveCustom))]
        [HarmonyPrefix]
        public static void SpellbookVM_RemoveCustom_Prefix(SpellbookVM __instance, AbilityData abilityData)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var ability = Main.Mapper.Map<NetworkAbility>(abilityData);
            var unitId = __instance.UnitDescriptor.Value.Unit.UniqueId;
            Main.Multiplayer.OnRemoveCustomSpell(unitId, ability);
        }

        [HarmonyPatch(typeof(SpellbookVM), nameof(SpellbookVM.TryMemorize))]
        [HarmonyPrefix]
        public static bool SpellbookVM_TryMemorize_Prefix(SpellbookVM __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canChangeSlot = Main.Multiplayer.IsControlledByLocalPlayer(__instance.UnitDescriptor.Value.Unit.UniqueId);
            if (!canChangeSlot)
            {
                Main.PlayerNotification.ShowWarningNotification(WellKnownKeys.GameNotifications.SpellBook.NoSpellSlotPermission.Key);
            }

            return canChangeSlot;
        }

        [HarmonyPatch(typeof(SpellbookVM), nameof(SpellbookVM.TryMemorize))]
        [HarmonyPostfix]
        public static void SpellbookVM_TryMemorize_Postfix(SpellbookVM __instance, AbilityDataVM abilityData, SpellSlot spellSlot, ref bool __result)
        {
            if (!Main.Multiplayer.IsActive || !__result)
            {
                return;
            }

            try
            {
                var unitId = __instance.UnitDescriptor.Value.Unit.UniqueId;
                var ability = Main.Mapper.Map<NetworkAbility>(abilityData.SpellData);
                var slot = Main.Mapper.Map<NetworkSpellSlot>(spellSlot);

                Main.Multiplayer.OnMemorizeSpell(unitId, slot, ability);
            }
            catch (Exception ex)
            {
                Main.GetLogger<SpellbookVMPatches>().LogError(ex, "Error while memorizing spell");
                throw;
            }
        }

        [HarmonyPatch(typeof(SpellbookVM), nameof(SpellbookVM.Swap))]
        [HarmonyPrefix]
        public static void SpellbookVM_Swap_Prefix(SpellbookVM __instance, SpellSlot slot1, SpellSlot slot2)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var spellSlotA = Main.Mapper.Map<NetworkSpellSlot>(slot1);
            var spellSlotB = Main.Mapper.Map<NetworkSpellSlot>(slot2);
            var spellLevel = slot1.SpellLevel;
            var spellbookId = __instance.CurrentSpellbook.Value.Blueprint.AssetGuid.ToString();
            var unitId = __instance.UnitDescriptor.Value.Unit.UniqueId;
            Main.Multiplayer.OnSwapMemorizedSlots(unitId, spellbookId, spellLevel, spellSlotA,  spellSlotB);
        }

        [HarmonyPatch(typeof(SpellbookVM), nameof(SpellbookVM.TryForget))]
        [HarmonyPrefix]
        public static bool SpellbookVM_TryForget_Prefix(SpellbookVM __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canChangeSlot = Main.Multiplayer.IsControlledByLocalPlayer(__instance.UnitDescriptor.Value.Unit.UniqueId);
            if (!canChangeSlot)
            {
                Main.PlayerNotification.ShowWarningNotification(WellKnownKeys.GameNotifications.SpellBook.NoSpellSlotPermission.Key);
            }

            return canChangeSlot;
        }

        [HarmonyPatch(typeof(SpellbookVM), nameof(SpellbookVM.TryForget))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> SpellbookVM_TryForget_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(Spellbook), nameof(Spellbook.ForgetMemorized));
            var replaceWith = AccessTools.Method(typeof(SpellbookVMPatches), nameof(SpellbookVMPatches.ForgetSpell));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<SpellbookVMPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldarg_1),
                new(OpCodes.Call, replaceWith),
            };
            match = match.Advance(1).Insert(newInstructions);
            Main.GetLogger<SpellbookVMPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static void ForgetSpell(SpellbookVM spellbookVM, SpellbookMemorizeSlotVM spellbookMemorizeVM)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            try
            {
                var unitId = spellbookVM.UnitDescriptor.Value.Unit.UniqueId;
                var ability = Main.Mapper.Map<NetworkAbility>(spellbookMemorizeVM.SpellData);
                var slot = Main.Mapper.Map<NetworkSpellSlot>(spellbookMemorizeVM.SpellSlot);

                Main.Multiplayer.OnForgetSpell(unitId, slot, ability);
            }
            catch (Exception ex)
            {
                Main.GetLogger<SpellbookVMPatches>().LogError(ex, "Error while forgetting spell");
                throw;
            }
        }
    }
}
