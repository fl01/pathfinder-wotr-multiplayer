using HarmonyLib;
using Kingmaker;
using Kingmaker.Assets.Controllers.GlobalMap;
using Kingmaker.Globalmap;
using Kingmaker.Globalmap.State;
using Kingmaker.Globalmap.View;
using Kingmaker.UI.GlobalMap;
using Kingmaker.UI.MVVM._PCView.Common;
using Kingmaker.UI.MVVM._PCView.GlobalMap.Message;
using Kingmaker.UI.MVVM._VM.GlobalMap.Message;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.MP.Entities.GlobalMap;

namespace WOTRMultiplayer.HarmonyPatches.GlobalMap
{
    [HarmonyPatch]
    public class GlobalMapRandomEncounterPatches
    {
        [HarmonyPatch(typeof(RandomEncountersController), nameof(RandomEncountersController.RollTravelEncounter))]
        [HarmonyPrefix]
        public static bool RandomEncountersController_RollTravelEncounter_Prefix(ref bool __result)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.OnGlobalMapBeforeRollTravelEncounter();
            if (!canContinue)
            {
                __result = false;
                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(GlobalMapRandomEncounterController), nameof(GlobalMapRandomEncounterController.OnRandomEncounterStarted))]
        [HarmonyPrefix]
        public static void GlobalMapRandomEncounterController_OnRandomEncounterStarted_Prefix()
        {
            Main.GetLogger<GlobalMapRandomEncounterPatches>().LogWarning("On Random Encounter");
        }

        [HarmonyPatch(typeof(GlobalMapPlayerState), nameof(GlobalMapPlayerState.StartTravel))]
        [HarmonyPrefix]
        public static void GlobalMapPlayerState_StartTravel_Prefix(GlobalMapTravelData travelData)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            // not sure if location is always available during act2+ travels due to navigation arrows
            var destination = new NetworkGlobalMapLocation
            {
                Id = travelData.To.Location.AssetGuid.ToString(),
                Name = travelData.To.Location.name,
            };

            Main.Multiplayer.OnGlobalMapStartTravel(destination);
        }

        [HarmonyPatch(typeof(GlobalMapPlayerState), nameof(GlobalMapPlayerState.FinishTravel))]
        [HarmonyPrefix]
        public static void GlobalMapPlayerState_FinishTravel_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.GetLogger<GlobalMapRandomEncounterPatches>().LogWarning("GlobalMapPlayerState_FinishTravel_Prefix");
        }

        [HarmonyPatch(typeof(GlobalMapMovementUtility), nameof(GlobalMapMovementUtility.ShowCollectIngredientMessage))]
        [HarmonyPostfix]
        public static void GlobalMapMovementUtility_ShowCollectIngredientMessage_Postfix(IGlobalMapTraveler traveler)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var modalMessage = (Game.Instance.RootUiContext.m_CommonView as CommonPCView).m_MessageModalPCView;
            modalMessage.m_AcceptButton.Interactable = false;
            var locationId = traveler.Location.AssetGuid.ToString();
            Main.GetLogger<GlobalMapRandomEncounterPatches>().LogWarning("Show ingridients confirmation. LocationId={LocationId}", locationId);
        }

        [HarmonyPatch(typeof(GlobalMapEnterMessagePCView), nameof(GlobalMapEnterMessagePCView.BindViewImplementation))]
        [HarmonyPostfix]
        public static void GlobalMapEnterMessagePCView_BindViewImplementation_Postfix(GlobalMapEnterMessagePCView __instance)
        {
            if (!Main.Multiplayer.IsActive || __instance.GetViewModel() is not GlobalMapEnterMessageVM messageVM || !messageVM.IsCurrentLocation)
            {
                __instance.m_AcceptButton.Interactable = true;
                return;
            }

            __instance.m_AcceptButton.Interactable = false;
            Main.GetLogger<GlobalMapRandomEncounterPatches>().LogWarning("Enter target location message box confirmation");
        }

        [HarmonyPatch(typeof(GlobalMapEnterMessageVM), nameof(GlobalMapEnterMessageVM.Close))]
        [HarmonyPrefix]
        public static void GlobalMapEnterMessageVM_Close_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.GetLogger<GlobalMapRandomEncounterPatches>().LogWarning("Close 'Enter location' message box");
        }

        [HarmonyPatch(typeof(GlobalMapEnterMessageVM), nameof(GlobalMapEnterMessageVM.CanLocationSelect))]
        [HarmonyPostfix]
        public static void GlobalMapEnterMessageVM_CanLocationSelect_Prefix(GlobalMapPointView locationView, ref bool __result)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var locationId = locationView.Blueprint.AssetGuid.ToString();
            var canSelectLocation = Main.Multiplayer.OnGlobalMapSelectLocation(locationId);
            __result = __result && canSelectLocation;
        }

        [HarmonyPatch(typeof(GlobalMapUI), nameof(GlobalMapUI.OnContinue))]
        [HarmonyPrefix]
        public static void GlobalMapUI_OnContinue_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var globalMapState = GetGlobalMapState();
            Main.Multiplayer.OnGlobalMapContinueTravel(globalMapState);
        }

        [HarmonyPatch(typeof(GlobalMapUI), nameof(GlobalMapUI.OnStop))]
        [HarmonyPrefix]
        public static void GlobalMapUI_OnStop_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var globalMapState = GetGlobalMapState();
            Main.Multiplayer.OnGlobalMapStopTravel(globalMapState);
        }

        private static NetworkGlobalMapState GetGlobalMapState()
        {
            var state = new NetworkGlobalMapState
            {
                Player = new NetworkGlobalMapTraveler
                {
                    Position = GetGlobalMapPosition(GlobalMapView.Instance.State.Player?.Position),
                },
            };
            return state;
        }

        private static NetworkGlobalMapPosition GetGlobalMapPosition(GlobalMapPosition globalMapPosition)
        {
            if (globalMapPosition == null)
            {
                return null;
            }

            var position = new NetworkGlobalMapPosition
            {
                Edge = globalMapPosition.EdgePosition
            };
            return position;
        }
    }
}
