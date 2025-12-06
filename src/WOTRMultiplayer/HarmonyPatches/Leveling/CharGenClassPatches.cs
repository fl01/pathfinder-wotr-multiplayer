using HarmonyLib;
using Kingmaker.Blueprints.Classes;
using Kingmaker.UI.MVVM._PCView.CharGen.Phases.Class;
using Kingmaker.UI.MVVM._VM.CharGen.Phases.Class;

namespace WOTRMultiplayer.HarmonyPatches.Leveling
{
    [HarmonyPatch]
    public class CharGenClassPatches
    {
        [HarmonyPatch(typeof(CharGenClassSelectorItemPCView), nameof(CharGenClassSelectorItemPCView.OnClick))]
        [HarmonyPrefix]
        public static bool CharGenClassSelectorItemPCView_OnClick_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return false;
            }

            var canContinue = Main.Multiplayer.CanMakeLevelingDecisions();
            return canContinue;
        }

        [HarmonyPatch(typeof(CharGenClassPhaseVM), nameof(CharGenClassPhaseVM.TryWarnChangeRace))]
        [HarmonyPostfix]
        public static void CharGenPhaseRoadmapBaseView_TryWarnChangeRace_Prefix(CharGenClassSelectorItemVM archetypeVM)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var archetypeId = archetypeVM?.Archetype.AssetGuid.ToString();
            Main.Multiplayer.OnLevelingClassArchetypeSelected(archetypeId);
        }

        [HarmonyPatch(typeof(CharGenClassPhaseVM), nameof(CharGenClassPhaseVM.OnMechanicClassSelected))]
        [HarmonyPostfix]
        public static void CharGenPhaseRoadmapBaseView_TryWarnChangeRace_Prefix(BlueprintCharacterClass selectedClass)
        {
            if (selectedClass == null)
            {
                return;
            }

            var classId = selectedClass?.AssetGuid.ToString();
            Main.Multiplayer.OnLevelingClassSelected(classId);
        }
    }
}
