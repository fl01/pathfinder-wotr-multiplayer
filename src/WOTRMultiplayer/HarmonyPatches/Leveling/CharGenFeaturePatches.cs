using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.CharGen.Phases.FeatureSelector;
using WOTRMultiplayer.Entities.Leveling;

namespace WOTRMultiplayer.HarmonyPatches.Leveling
{
    [HarmonyPatch]
    public class CharGenFeaturePatches
    {
        [HarmonyPatch(typeof(CharGenFeatureSelectorItemPCView), nameof(CharGenFeatureSelectorItemPCView.OnClick))]
        [HarmonyPrefix]
        public static bool CharGenFeatureSelectorItemPCView_OnClick_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.CanMakeLevelingDecisions();
            return canContinue;
        }

        [HarmonyPatch(typeof(CharGenFeatureSelectorItemPCView), nameof(CharGenFeatureSelectorItemPCView.ApplyOnClick))]
        [HarmonyPostfix]
        public static void CharGenFeatureSelectorItemPCView_ApplyOnClick_Postfix(CharGenFeatureSelectorItemPCView __instance)
        {
            if (!Main.Multiplayer.IsActive || __instance.ViewModel == null)
            {
                return;
            }

            var feature = new NetworkLevelingFeature
            {
                Id = __instance.ViewModel.Feature.Feature.AssetGuid.ToString(),
                Name = __instance.ViewModel.Feature.NameForAcronym
            };

            Main.Multiplayer.OnLevelingFeatureSelected(feature);
        }
    }
}
