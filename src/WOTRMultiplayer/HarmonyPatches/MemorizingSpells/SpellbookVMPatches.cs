using HarmonyLib;
using Kingmaker.Localization;
using Kingmaker.PubSubSystem;
using Kingmaker.UI.MVVM._VM.ServiceWindows.Spellbook;
using Kingmaker.UI.MVVM._VM.ServiceWindows.Spellbook.KnownSpells;
using Kingmaker.UI.MVVM._VM.ServiceWindows.Spellbook.MemorizingPanel;
using Kingmaker.UnitLogic;
using WOTRMultiplayer.Entities.Spells;

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
                EventBus.RaiseEvent<IWarningNotificationUIHandler>(x => x.HandleWarning(message, true), true);
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

            var slot = new NetworkSpellSlot
            {
                Index = spellSlot?.Index,
                Type = spellSlot?.Type,
                SpellbookId = __instance.SpellbookInformationVM.Spellbook.Value.Blueprint.Name.Key,
                SpellId = abilityData?.SpellData.UniqueId,
                SpellName = abilityData?.SpellData.NameForAcronym,
                SpellLevel = abilityData.SpellLevel,
                UnitId = __instance.UnitDescriptor.Value.Unit.UniqueId

            };

            Main.Multiplayer.OnMemorizeSpell(slot);
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
                EventBus.RaiseEvent<IWarningNotificationUIHandler>(x => x.HandleWarning(message, true), true);
            }
            return canChangeSlot;
        }

        [HarmonyPatch(typeof(SpellbookVM), nameof(SpellbookVM.TryForget))]
        [HarmonyPostfix]
        public static void SpellbookVM_TryForget_Postfix(SpellbookVM __instance, SpellbookMemorizeSlotVM spellSlot, ref bool __result)
        {
            if (!Main.Multiplayer.IsActive || !__result)
            {
                return;
            }

            var slot = new NetworkSpellSlot
            {
                Index = spellSlot.SpellSlot.Index,
                Type = spellSlot.SpellSlot.Type,
                SpellbookId = __instance.SpellbookInformationVM.Spellbook.Value.Blueprint.Name.Key,
                UnitId = __instance.UnitDescriptor.Value.Unit.UniqueId,
                SpellLevel = spellSlot.SpellLevel,
            };

            Main.Multiplayer.OnForgetSpell(slot);
        }
    }
}
