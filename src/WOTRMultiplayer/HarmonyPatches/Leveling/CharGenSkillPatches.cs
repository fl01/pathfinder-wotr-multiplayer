using HarmonyLib;
using Kingmaker.UI.MVVM._CommonView.CharGen.Phases.Skills;
using Kingmaker.UI.MVVM._VM.CharGen.Phases.Skills;
using WOTRMultiplayer.Entities.Leveling;

namespace WOTRMultiplayer.HarmonyPatches.Leveling
{
    [HarmonyPatch]
    public class CharGenSkillPatches
    {
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
