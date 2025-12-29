using HarmonyLib;
using Kingmaker.UI.MVVM._CommonView.CharGen.Phases.Skills;
using Kingmaker.UI.MVVM._PCView.CharGen.Phases;
using Kingmaker.UI.MVVM._VM.CharGen.Phases;
using Kingmaker.UI.MVVM._VM.CharGen.Phases.Skills;
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

            var charGenView = CharGenViewAccessor.GetCharGenContextView()?.m_CharGenPCView;
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

        [HarmonyPatch(typeof(CharGenSkillAllocatorCommonView), nameof(CharGenSkillAllocatorCommonView.OnUpButton))]
        [HarmonyPrefix]
        public static bool CharGenSkillAllocatorCommonView_OnUpButton_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canInteract = Main.Multiplayer.CanMakeLevelingDecisions();
            return canInteract;
        }

        [HarmonyPatch(typeof(CharGenSkillAllocatorCommonView), nameof(CharGenSkillAllocatorCommonView.OnDownButton))]
        [HarmonyPrefix]
        public static bool CharGenSkillAllocatorCommonView_OnDownButton_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canInteract = Main.Multiplayer.CanMakeLevelingDecisions();
            return canInteract;
        }

        [HarmonyPatch(typeof(CharGenSkillAllocatorVM), nameof(CharGenSkillAllocatorVM.TryIncreaseValue))]
        [HarmonyPrefix]
        public static void CharGenSkillAllocatorVM_TryIncreaseValue_Prefix(CharGenSkillAllocatorVM __instance)
        {
            if (!Main.Multiplayer.IsActive || !__instance.CanAdd.Value)
            {
                return;
            }

            var skill = new NetworkLevelingSkillPoint
            {
                StatType = __instance.StatType
            };

            Main.Multiplayer.OnLevelingIncreaseSkillPoint(skill);
        }

        [HarmonyPatch(typeof(CharGenSkillAllocatorVM), nameof(CharGenSkillAllocatorVM.TryDecreaseValue))]
        [HarmonyPrefix]
        public static void CharGenSkillAllocatorVM_TryDecreaseValue_Prefix(CharGenSkillAllocatorVM __instance)
        {
            if (!Main.Multiplayer.IsActive || !__instance.CanRemove.Value)
            {
                return;
            }

            var skill = new NetworkLevelingSkillPoint
            {
                StatType = __instance.StatType
            };

            Main.Multiplayer.OnLevelingDecreaseSkillPoint(skill);
        }
    }
}
