using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.Localization;
using Kingmaker.PubSubSystem;
using Kingmaker.UI.MVVM._VM.ServiceWindows.Spellbook;
using Kingmaker.UI.MVVM._VM.ServiceWindows.Spellbook.KnownSpells;
using Kingmaker.UI.MVVM._VM.ServiceWindows.Spellbook.MemorizingPanel;
using Kingmaker.UnitLogic;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.Spells;
using WOTRMultiplayer.HarmonyPatches.Combat;

namespace WOTRMultiplayer.HarmonyPatches.MemorizingSpells
{
    [HarmonyPatch]
    public class SpellbookVMPatches
    {
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
                var message = new LocalizedString { Key = WellKnownKeys.GameNotifications.SpellBook.NoSpellSlotPermission.Key };
                EventBus.RaiseEvent<IWarningNotificationUIHandler>(x => x.HandleWarning(message, true));
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
                Main.GetLogger<UnitFearControllerPatches>().LogError(ex, "Error while memorizing spell");
                throw;
            }
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
                var message = new LocalizedString { Key = WellKnownKeys.GameNotifications.SpellBook.NoSpellSlotPermission.Key };
                EventBus.RaiseEvent<IWarningNotificationUIHandler>(x => x.HandleWarning(message, true));
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
            Main.GetLogger<SpellbookVMPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
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
                Main.GetLogger<UnitFearControllerPatches>().LogError(ex, "Error while forgetting spell");
                throw;
            }
        }
    }
}
