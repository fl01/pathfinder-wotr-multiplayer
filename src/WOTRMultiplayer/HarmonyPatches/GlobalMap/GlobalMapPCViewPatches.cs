using System;
using HarmonyLib;
using Kingmaker.Armies;
using Kingmaker.Armies.Blueprints;
using Kingmaker.Globalmap;
using Kingmaker.Globalmap.State;
using Kingmaker.UI.MVVM._PCView.GlobalMap;
using Microsoft.Extensions.Logging;
using UniRx;

namespace WOTRMultiplayer.HarmonyPatches.GlobalMap
{
    /// <summary>
    /// TODO: delete once army management is done
    /// </summary>
    [HarmonyPatch]
    public class GlobalMapPCViewPatches
    {
        /// <summary>
        /// All armies loaded from save
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPatch(typeof(GlobalMapPCView), nameof(GlobalMapPCView.BindViewImplementation))]
        [HarmonyPostfix]
        public static void GlobalMapPCView_BindViewImplementation_Postfix(GlobalMapPCView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            foreach (var army in GlobalMapController.State?.Armies ?? [])
            {
                OnStartTrackingArmyStateChanged(__instance, army);
            }
        }

        /// <summary>
        /// All armies created while playing
        /// </summary>
        /// <param name="__result"></param>
        [HarmonyPatch(typeof(GlobalMapState), nameof(GlobalMapState.CreateArmy), [typeof(ArmyFaction), typeof(BlueprintArmyPreset), typeof(GlobalMapPosition), typeof(bool), typeof(bool)])]
        [HarmonyPostfix]
        public static void GlobalMapState_CreateArmy_Postfix(ref GlobalMapArmyState __result)
        {
            if (!Main.Multiplayer.IsActive || Main.UIAccessor.GlobalMapPCView?.ViewModel == null)
            {
                return;
            }

            OnStartTrackingArmyStateChanged(Main.UIAccessor.GlobalMapPCView, __result);
        }

        private static void OnStartTrackingArmyStateChanged(GlobalMapPCView globalMapView, GlobalMapArmyState globalMapArmyState)
        {
            try
            {
                if (globalMapArmyState == null || globalMapArmyState.Data.Faction != ArmyFaction.Crusaders)
                {
                    return;
                }

                globalMapView.AddDisposable(globalMapArmyState.Data.OnUnitChangedTrigger.Subscribe(_ => OnCrusadeArmySquadsChanged(globalMapArmyState)));
                Main.GetLogger<GlobalMapPCViewPatches>().LogInformation("Started tracking army unit changes. ArmyId={ArmyId}", globalMapArmyState.Id);
            }
            catch (Exception ex)
            {
                Main.GetLogger<GlobalMapPCViewPatches>().LogError(ex, "Error while starting to track army state. ArmyId={ArmyId}", globalMapArmyState?.Id);
                throw;
            }
        }

        private static void OnCrusadeArmySquadsChanged(GlobalMapArmyState armyState)
        {
            try
            {
                if ((armyState.Data?.Squads?.Count ?? 0) == 0)
                {
                    return;
                }

                //var army = Create(armyState);
                //Main.GetLogger<GlobalMapPCViewPatches>().LogInformation("Crusade army squads changed. ArmyId={ArmyId}, Squads={Squads}", army.Id, army.SquadPositions);
            }
            catch (Exception ex)
            {
                Main.GetLogger<GlobalMapPCViewPatches>().LogError(ex, "Unable to handle squad change event. ArmyId={ArmyId}", armyState?.Id);
                throw;
            }
        }
    }
}
