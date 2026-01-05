using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.CharGen.Phases;
using Kingmaker.UI.MVVM._VM.CharGen.Phases;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.SelectionGroup;
using WOTRMultiplayer.Entities.Leveling;

namespace WOTRMultiplayer.HarmonyPatches.Leveling
{
    [HarmonyPatch]
    public class CharGenPhaseDetailedBaseViewPatches
    {
        [HarmonyPatch(typeof(CharGenPhaseDetailedBaseView<CharGenPhaseBaseVM>), nameof(CharGenPhaseDetailedBaseView<CharGenPhaseBaseVM>.Show))]
        [HarmonyPrefix]
        public static void CharInfoLevelClassScoresPCView_Prefix(CharGenPhaseDetailedBaseView<CharGenPhaseBaseVM> __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var charGenView = Main.UIAccessor.CharGenView;
            if (charGenView == null)
            {
                Main.GetLogger<CharGenPhaseDetailedBaseViewPatches>().LogError("Unable to find char gen pc view");
                return;
            }

            var roadMapVM = charGenView.RoadmapMenuView.GetViewModel() as SelectionGroupRadioVM<CharGenPhaseBaseVM>;
            var phaseVM = __instance.GetViewModel() as CharGenPhaseBaseVM;
            var phaseIndex = roadMapVM.EntitiesCollection.IndexOf(phaseVM);
            var phase = new NetworkLevelingPhase
            {
                Index = phaseIndex
            };
            Main.Multiplayer.OnWitnessLevelingPhase(phase);
        }

        [HarmonyPatch(typeof(CharGenPhaseRoadmapBaseView<CharGenPhaseBaseVM>), nameof(CharGenPhaseRoadmapBaseView<CharGenPhaseBaseVM>.UpdateSelectableState))]
        [HarmonyPostfix]
        public static void CharGenPhaseRoadmapBaseView_UpdateSelectableState_Postfix(CharGenPhaseRoadmapBaseView<CharGenPhaseBaseVM> __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var canInteract = Main.Multiplayer.CanMakeLevelingDecisions();
            __instance.m_Button.Interactable = __instance.m_Button.Interactable && canInteract;
            __instance.m_ButtonBackground.Interactable = __instance.m_ButtonBackground.Interactable && canInteract;
            __instance.m_ButtonLabel.Interactable = __instance.m_ButtonLabel.Interactable && canInteract;
        }
    }
}
