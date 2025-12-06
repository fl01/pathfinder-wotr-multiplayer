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

        [HarmonyPatch(typeof(CharGenVM), nameof(CharGenVM.Complete))]
        [HarmonyPostfix]
        public static void CharGenVM_Complete_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnLevelingCompleted();
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
