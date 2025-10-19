using HarmonyLib;
using Kingmaker.Assets.Controllers.GlobalMap;
using Kingmaker.Globalmap.State;
using Kingmaker.UI.GlobalMap;
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

        [HarmonyPatch(typeof(GlobalMapMessageBox), nameof(GlobalMapMessageBox.OnLocationSelect))]
        [HarmonyPrefix]
        public static void GlobalMapMessageBox_OnLocationSelect_Prefix()
        {
            Main.GetLogger<GlobalMapRandomEncounterPatches>().LogWarning("GlobalMapMessageBox_OnLocationSelect_Prefix");
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

            var locationId = traveler.Location.AssetGuid.ToString();
            //Main.Multiplayer.OnGlobalMapAfterShowCollectIngredient(locationId);
            Main.GetLogger<GlobalMapRandomEncounterPatches>().LogWarning("OnGlobalMapAfterShowCollectIngredient. LocationId={LocationId}", locationId);
        }
    }
}
