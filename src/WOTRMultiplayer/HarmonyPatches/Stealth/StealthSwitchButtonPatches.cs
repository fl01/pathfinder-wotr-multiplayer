using HarmonyLib;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI.ActionBar;
using Kingmaker.UI.MVVM._VM.ActionBar;
using Kingmaker.UI.MVVM._VM.Party;

namespace WOTRMultiplayer.HarmonyPatches.Stealth
{
    [HarmonyPatch]
    public class StealthSwitchButtonPatches
    {
        [HarmonyPatch(typeof(StealthSwitchButton), nameof(StealthSwitchButton.SetStealthEnabled))]
        [HarmonyPrefix]
        public static void StealthSwitchButton_SetStealthEnabled_Prefix(UnitEntityData unit, bool enabled)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnSetUnitStealthEnabled(unit.UniqueId, enabled, isForced: false);
        }

        [HarmonyPatch(typeof(ControlCharactersVM), nameof(ControlCharactersVM.SetStealthEnabled))]
        [HarmonyPrefix]
        public static void ControlCharactersVM_SetStealthEnabled_Prefix(UnitEntityData unit, bool enabled)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnSetUnitStealthEnabled(unit.UniqueId, enabled, isForced: false);
        }

        [HarmonyPatch(typeof(PartyCharacterVM), nameof(PartyCharacterVM.SetStealth))]
        [HarmonyPrefix]
        public static void PartyCharacterVM_SetStealth_Prefix(PartyCharacterVM __instance, bool enabled)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnSetUnitStealthEnabled(__instance.UnitEntityData.UniqueId, enabled, isForced: false);
        }
    }
}
