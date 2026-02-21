using HarmonyLib;
using Kingmaker.Kingdom.Settlements;
using Kingmaker.UI.Settlement;

namespace WOTRMultiplayer.HarmonyPatches.GlobalMap.Kingdom
{
    [HarmonyPatch]
    public class SettlementsBuildSlotsPatches
    {
        [HarmonyPatch(typeof(SettlementsBuildSlots), nameof(SettlementsBuildSlots.OnSlotClick))]
        [HarmonyPrefix]
        public static bool SettlementsBuildSlots_OnSlotClick_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.CanControlGlobalMap();
            return canContinue;
        }

        [HarmonyPatch(typeof(SettlementBuildingWindow), nameof(SettlementBuildingWindow.SetContent))]
        [HarmonyPostfix]
        public static void SettlementBuildingWindow_SetContent_Postfix(SettlementBuildingWindow __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var canContinue = Main.Multiplayer.CanControlGlobalMap();
            if (!canContinue)
            {
                __instance.m_SellBlock.SellBuild.interactable = false;
            }
        }
    }
}
