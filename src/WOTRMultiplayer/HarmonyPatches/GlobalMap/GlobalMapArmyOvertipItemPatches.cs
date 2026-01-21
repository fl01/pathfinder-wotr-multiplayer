using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.UI.MVVM._PCView.Crusade.Overtips;
using Kingmaker.UI.MVVM._VM.Crusade.Overtips;

namespace WOTRMultiplayer.HarmonyPatches.GlobalMap
{
    [HarmonyPatch]
    public class GlobalMapArmyOvertipItemPatches
    {
        [HarmonyPatch(typeof(GlobalMapArmyOvertipItemPCView), nameof(GlobalMapArmyOvertipItemPCView.CheckMerge))]
        [HarmonyPostfix]
        public static void GlobalMapArmyOvertipItemPCView_CheckMerge_Postfix(GlobalMapArmyOvertipItemPCView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            __instance.m_MergeButton.Interactable = Main.Multiplayer.CanNavigateOnGlobalMap();
        }

        [HarmonyPatch(typeof(GlobalMapArmyOvertipsVM), nameof(GlobalMapArmyOvertipsVM.MergeArmies))]
        [HarmonyPrefix]
        public static void GlobalMapArmyOvertipsVM_MergeArmies_Prefix(GlobalMapArmyOvertipsVM __instance)
        {
            var selectedArmy = Game.Instance.GlobalMapController.SelectedArmy;
            if (!Main.Multiplayer.IsActive || selectedArmy == null || selectedArmy.View == null || !__instance.m_ArmiesForMerge.Any())
            {
                return;
            }

            Main.Multiplayer.OnGlobalMapMergeArmies();
        }
    }
}
