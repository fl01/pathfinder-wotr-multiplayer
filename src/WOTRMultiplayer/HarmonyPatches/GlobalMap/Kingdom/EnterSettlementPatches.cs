using HarmonyLib;
using Kingmaker.Kingdom;
using Kingmaker.UI.Kingdom;
using Kingmaker.UI.MVVM._VM.GlobalMap.Message;
using Kingmaker.UI.MVVM._VM.Kingdom.Settlements;
using WOTRMultiplayer.Entities.GlobalMap.Kingdom;

namespace WOTRMultiplayer.HarmonyPatches.GlobalMap.Kingdom
{
    [HarmonyPatch]
    public class EnterSettlementPatches
    {
        [HarmonyPatch(typeof(KingdomSettlementDetailsVM), nameof(KingdomSettlementDetailsVM.EnterSettlement))]
        [HarmonyPrefix]
        public static void KingdomSettlementDetailsVM_EnterSettlement_Prefix(KingdomSettlementDetailsVM __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var kingdomSettlement = Main.Mapper.Map<NetworkKingdomSettlement>(__instance.Settlement.Value);
            var requiresUnloadEvent = true;
            var exitSettlementToGlobalMap = KingdomState.Instance.ExitSettlementToGlobalMap;
            Main.Multiplayer.OnKingdomEnterSettlement(kingdomSettlement, requiresUnloadEvent, exitSettlementToGlobalMap);
        }

        [HarmonyPatch(typeof(GlobalMapEnterMessageVM), nameof(GlobalMapEnterMessageVM.AlternativeAction))]
        [HarmonyPrefix]
        public static void GlobalMapEnterMessageVM_AlternativeAction_Prefix(GlobalMapEnterMessageVM __instance)
        {
            if (!Main.Multiplayer.IsActive || __instance.Settlement == null)
            {
                return;
            }

            var kingdomSettlement = Main.Mapper.Map<NetworkKingdomSettlement>(__instance.Settlement);
            var requiresUnloadEvent = false;
            var exitSettlementToGlobalMap = true;
            Main.Multiplayer.OnKingdomEnterSettlement(kingdomSettlement, requiresUnloadEvent, exitSettlementToGlobalMap);
        }

        [HarmonyPatch(typeof(KingdomUISettlementWindow), nameof(KingdomUISettlementWindow.OnEnterClick))]
        [HarmonyPrefix]
        public static void KingdomUISettlementWindow_OnEnterClick_Prefix(KingdomUISettlementWindow __instance)
        {
            if (!Main.Multiplayer.IsActive || __instance.m_Marker == null)
            {
                return;
            }

            var kingdomSettlement = Main.Mapper.Map<NetworkKingdomSettlement>(__instance.m_Marker.Settlement);
            var requiresUnloadEvent = true;
            var exitSettlementToGlobalMap = KingdomState.Instance.ExitSettlementToGlobalMap;
            Main.Multiplayer.OnKingdomEnterSettlement(kingdomSettlement, requiresUnloadEvent, exitSettlementToGlobalMap);
        }
    }
}
