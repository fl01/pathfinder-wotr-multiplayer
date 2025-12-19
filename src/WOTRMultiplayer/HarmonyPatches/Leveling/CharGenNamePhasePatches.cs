using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.CharGen.Phases.Name;
using Kingmaker.UI.MVVM._VM.CharGen.Phases.Name;

namespace WOTRMultiplayer.HarmonyPatches.Leveling
{
    [HarmonyPatch]
    public class CharGenNamePhasePatches
    {
        [HarmonyPatch(typeof(CharGenNamePhaseDetailedPCView), nameof(CharGenNamePhaseDetailedPCView.OnGenerateButtonClick))]
        [HarmonyPrefix]
        public static bool CharGenNamePhaseDetailedPCView_OnGenerateButtonClick_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.CanMakeLevelingDecisions();
            return canContinue;
        }

        [HarmonyPatch(typeof(CharGenNamePhaseVM), nameof(CharGenNamePhaseVM.OnEndEdit))]
        [HarmonyPostfix]
        public static void CharGenNamePhaseVM_OnEndEdit_Postfix(CharGenNamePhaseVM __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var name = __instance.m_TextResult;
            Main.Multiplayer.OnLevelingNameChanged(name);
        }
    }
}
