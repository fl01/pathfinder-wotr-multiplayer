using HarmonyLib;
using Kingmaker.UI.MVVM._CommonView.CharGen.Phases.AbilityScores;
using Kingmaker.UI.MVVM._CommonView.CharGen.Phases.Skills;
using Kingmaker.UI.MVVM._VM.CharGen.Phases.AbilityScores;
using WOTRMultiplayer.Entities.Leveling;

namespace WOTRMultiplayer.HarmonyPatches.Leveling
{
    [HarmonyPatch]
    public class CharGenAbilityScorePatches
    {
        [HarmonyPatch(typeof(CharGenAbilityScoreAllocatorCommonView), nameof(CharGenSkillAllocatorCommonView.OnUpButton))]
        [HarmonyPrefix]
        public static bool CharGenAbilityScoreAllocatorCommonView_OnUpButton_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canInteract = Main.Multiplayer.CanMakeLevelingDecisions();
            return canInteract;
        }

        [HarmonyPatch(typeof(CharGenAbilityScoreAllocatorCommonView), nameof(CharGenSkillAllocatorCommonView.OnDownButton))]
        [HarmonyPrefix]
        public static bool CharGenAbilityScoreAllocatorCommonView_OnDownButton_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canInteract = Main.Multiplayer.CanMakeLevelingDecisions();
            return canInteract;
        }

        [HarmonyPatch(typeof(CharGenAbilityScoreAllocatorVM), nameof(CharGenAbilityScoreAllocatorVM.TryIncreaseValue))]
        [HarmonyPrefix]
        public static void CharGenAbilityScoreAllocatorVM_TryIncreaseValue_Prefix(CharGenAbilityScoreAllocatorVM __instance)
        {
            if (!Main.Multiplayer.IsActive || !__instance.CanAdd.Value)
            {
                return;
            }

            var abilityScore = new NetworkLevelingAbilityScore
            {
                StatType = __instance.StatType
            };

            Main.Multiplayer.OnLevelingIncreaseAbilityScore(abilityScore);
        }

        [HarmonyPatch(typeof(CharGenAbilityScoreAllocatorVM), nameof(CharGenAbilityScoreAllocatorVM.TryDecreaseValue))]
        [HarmonyPrefix]
        public static void CharGenAbilityScoreAllocatorVM_TryDecreaseValue_Prefix(CharGenAbilityScoreAllocatorVM __instance)
        {
            if (!Main.Multiplayer.IsActive || !__instance.CanRemove.Value)
            {
                return;
            }

            var abilityScore = new NetworkLevelingAbilityScore
            {
                StatType = __instance.StatType
            };

            Main.Multiplayer.OnLevelingDecreaseAbilityScore(abilityScore);
        }
    }
}
