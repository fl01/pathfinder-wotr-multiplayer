using System.Linq;
using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.CharGen;
using Kingmaker.UI.MVVM._VM.CharGen;

namespace WOTRMultiplayer.HarmonyPatches.Leveling
{
    [HarmonyPatch]
    public class CharGenPCViewPatches
    {
        [HarmonyPatch(typeof(CharGenVM), nameof(CharGenVM.Close))]
        [HarmonyPostfix]
        public static void CharGenVM_Close_Postfix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnLevelingTerminated();
        }

        [HarmonyPatch(typeof(CharGenContextVM), nameof(CharGenContextVM.TryGetPetNeedChargen))]
        [HarmonyPrefix]
        public static bool CharGenContextVM_TryGetPetNeedChargen_Prefix()
        {
            // autopening pet leveling is not needed
            return !Main.Multiplayer.IsActive;
        }

        [HarmonyPatch(typeof(CharGenVM), nameof(CharGenVM.Complete))]
        [HarmonyPrefix]
        public static void CharGenVM_Complete_Prefix(CharGenVM __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnLevelingCompleted();

            // base game has a bug where pet ui frames are not updated after leveling
            var leveledUnit = __instance.LevelUpConfig?.Unit;
            if (leveledUnit != null && leveledUnit.IsPet)
            {
                var petPartyView = Main.UIAccessor.PartyPCView?.m_Characters.FirstOrDefault(x => x.ViewModel != null && string.Equals(x.ViewModel.UnitEntityData.UniqueId, leveledUnit.UniqueId, System.StringComparison.OrdinalIgnoreCase));
                petPartyView?.ViewModel?.UpdateLevelUpField(leveledUnit);
            }
        }

        [HarmonyPatch(typeof(CharGenView), nameof(CharGenView.CloseCharGen))]
        [HarmonyPrefix]
        public static bool CharGenView_CloseCharGen_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canMakeLevelingDecisions = Main.Multiplayer.CanMakeLevelingDecisions();
            return canMakeLevelingDecisions;
        }

        [HarmonyPatch(typeof(CharGenPCView), nameof(CharGenPCView.SetActiveNextPhaseButton))]
        [HarmonyPostfix]
        public static void CharGenPCView_SetActiveNextPhaseButton_Postfix(CharGenPCView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var canMakeLevelingDecisions = Main.Multiplayer.CanMakeLevelingDecisions();
            var isInteractable = __instance.CanGoNext.Value && canMakeLevelingDecisions;
            __instance.m_NextButton.Interactable = isInteractable;
            __instance.m_NextValidPageButton.Interactable = isInteractable;
        }

        [HarmonyPatch(typeof(CharGenPCView), nameof(CharGenPCView.SetActiveBackPhaseButton))]
        [HarmonyPostfix]
        public static void CharGenPCView_SetActiveBackPhaseButton_Postfix(CharGenPCView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var canMakeLevelingDecisions = Main.Multiplayer.CanMakeLevelingDecisions();
            var isInteractable = __instance.CanGoBack.Value && canMakeLevelingDecisions;
            __instance.m_BackButton.Interactable = isInteractable;
            __instance.m_FirstPageButton.Interactable = isInteractable;
        }

    }
}
