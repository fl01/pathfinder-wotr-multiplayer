using HarmonyLib;
using Kingmaker.Kingdom.UI;
using Kingmaker.UI.MVVM._PCView.CityBuilder;
using Kingmaker.UI.MVVM._VM.CityBuilder;

namespace WOTRMultiplayer.HarmonyPatches.GlobalMap.Kingdom
{
    [HarmonyPatch]
    public class CityBuilderPatches
    {
        [HarmonyPatch(typeof(CityBuilderPCView), nameof(CityBuilderPCView.BindViewImplementation))]
        [HarmonyPrefix]
        public static void CityBuilderPCView_BindViewImplementation_Postfix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnKingdomSettlementLoaded();
        }

        [HarmonyPatch(typeof(CityBuilderVM), nameof(CityBuilderVM.LeaveCityBuilder))]
        [HarmonyPrefix]
        public static void CityBuilderVM_LeaveCityBuilder_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnKingdomLeaveSettlement();
        }

        [HarmonyPatch(typeof(CityBuilderUIBack), nameof(CityBuilderUIBack.BackToMap))]
        [HarmonyPrefix]
        public static bool CityBuilderUIBack_BackToMap_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            if (!Main.Multiplayer.CanControlGlobalMap())
            {
                return false;
            }

            Main.Multiplayer.OnKingdomLeaveSettlement();
            return true;
        }
    }
}
