using HarmonyLib;
using Kingmaker.UI.MVVM._VM.CharGen.Phases.Portrait;

namespace WOTRMultiplayer.HarmonyPatches.Leveling
{
    [HarmonyPatch]
    public class CharGenPortraitPatches
    {
        [HarmonyPatch(typeof(CharGenPortraitSelectorItemVM), nameof(CharGenPortraitSelectorItemVM.OnCustomPortraitCreate))]
        [HarmonyPrefix]
        public static bool CharGenPortraitSelectorItemVM_OnCustomPortraitCreate_Prefix(CharGenPortraitSelectorItemVM __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.CanMakeLevelingDecisions();
            return canContinue;
        }
    }
}
