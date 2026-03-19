using System;
using HarmonyLib;
using Kingmaker;
using Kingmaker.AreaLogic.Cutscenes;
using Kingmaker.Controllers;
using Kingmaker.View.Roaming;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches.Roaming
{
    [HarmonyPatch]
    public class RoamingWaypointDataPatches
    {
        [HarmonyPatch(typeof(RoamingWaypointData), nameof(RoamingWaypointData.SelectCutscene))]
        [HarmonyPrefix]
        public static bool RoamingWaypointData_SelectCutscene_Prefix(RoamingWaypointData __instance, ref Cutscene __result)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            try
            {
                var maxExclusive = __instance.WaypointView.IdleCutscenes.Count;
                if (maxExclusive == 0)
                {
                    return false;
                }

                var seededContext = Main.Multiplayer.GetSeededContext();
                var identifier = $"{nameof(RoamingWaypointData)}:{nameof(RoamingWaypointData.SelectCutscene)}:{Game.Instance.CurrentlyLoadedArea.name}:{__instance.UniqueId}_{seededContext.Id}";
                int index = Main.Multiplayer.ValueGenerator.Range(seededContext.Lifetime, identifier, 0, maxExclusive);
                var cutscene = __instance.WaypointView.IdleCutscenes[index];
                __result = cutscene.Get();

                Main.GetLogger<RoamingWaypointDataPatches>().LogDebug("Selected cutscene. Identifier={Identifier}, Name={Name}, MaxExclusive={MaxExclusive}", identifier, __result?.name, maxExclusive);
                return false;
            }
            catch (Exception ex)
            {
                Main.GetLogger<RoamingWaypointDataPatches>().LogError(ex, "Unable to select next cutscene");
                throw;
            }
        }

        [HarmonyPatch(typeof(RoamingWaypointData), nameof(RoamingWaypointData.SelectPrevPoint))]
        [HarmonyPrefix]
        public static bool RoamingWaypointData_SelectPrevPoint_Prefix(RoamingWaypointData __instance, ref IRoamingPoint __result)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            try
            {
                var maxExclusive = __instance.WaypointView.PrevWaypoints.Count;
                if (maxExclusive == 0)
                {
                    return false;
                }

                var seededContext = Main.Multiplayer.GetSeededContext();
                var identifier = $"{nameof(RoamingWaypointData)}:{nameof(RoamingWaypointData.SelectPrevPoint)}:{Game.Instance.CurrentlyLoadedArea.name}:{__instance.UniqueId}_{seededContext.Id}";
                int index = Main.Multiplayer.ValueGenerator.Range(seededContext.Lifetime, identifier, 0, maxExclusive);
                var waypoint = __instance.WaypointView.PrevWaypoints[index];
                __result = waypoint.WaypointData;

                Main.GetLogger<RoamingWaypointDataPatches>().LogDebug("Selected previous waypoint. Identifier={Identifier}, Position={Position}, MaxExclusive={MaxExclusive}", identifier, __result?.Position, maxExclusive);
                return false;
            }
            catch (Exception ex)
            {
                Main.GetLogger<RoamingWaypointDataPatches>().LogError(ex, "Unable to select previous roaming waypoint");
                throw;
            }
        }

        [HarmonyPatch(typeof(RoamingWaypointData), nameof(RoamingWaypointData.SelectNextPoint))]
        [HarmonyPrefix]
        public static bool RoamingWaypointData_SelectNextPoint_Prefix(RoamingWaypointData __instance, ref IRoamingPoint __result)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            try
            {
                var maxExclusive = __instance.WaypointView.NextWaypoints.Count;
                if (maxExclusive == 0)
                {
                    return false;
                }

                var seededContext = Main.Multiplayer.GetSeededContext();
                var identifier = $"{nameof(RoamingWaypointData)}:{nameof(RoamingWaypointData.SelectNextPoint)}:{Game.Instance.CurrentlyLoadedArea.name}:{__instance.UniqueId}_{seededContext.Id}";
                int nextWaypointIndex = Main.Multiplayer.ValueGenerator.Range(seededContext.Lifetime, identifier, 0, maxExclusive);
                NextWaypointEntry nextWaypointEntry = __instance.WaypointView.NextWaypoints[nextWaypointIndex];
                __result = nextWaypointEntry.Waypoint?.WaypointData;

                Main.GetLogger<RoamingWaypointDataPatches>().LogDebug("Selected next waypoint. Identifier={Identifier}, Position={Position}, MaxExclusive={MaxExclusive}", identifier, __result?.Position, maxExclusive);
                return false;
            }
            catch (Exception ex)
            {
                Main.GetLogger<RoamingWaypointDataPatches>().LogError(ex, "Unable to select next roaming waypoint");
                throw;
            }
        }

        [HarmonyPatch(typeof(RoamingWaypointData), nameof(RoamingWaypointData.SelectIdleTime))]
        [HarmonyPrefix]
        public static bool RoamingWaypointData_SelectIdleTime_Prefix(RoamingWaypointData __instance, ref TimeSpan __result)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            try
            {
                var seededContext = Main.Multiplayer.GetSeededContext();
                var identifier = $"{nameof(RoamingWaypointData)}:{nameof(RoamingWaypointData.SelectIdleTime)}:{Game.Instance.CurrentlyLoadedArea.name}:{__instance.UniqueId}_{seededContext.Id}";
                float idleTime = Main.Multiplayer.ValueGenerator.Range(seededContext.Lifetime, identifier, __instance.WaypointView.MinIdleTime, __instance.WaypointView.MaxIdleTime);
                __result = idleTime.Seconds();

                Main.GetLogger<RoamingWaypointDataPatches>().LogDebug("Selected idle time. Identifier={Identifier}, RawTime={RawTime}, Time={Time}, MinTimeRange={MinTimeRange}, MaxTimeRange={MaxTimeRange}", identifier, idleTime, __result, __instance.WaypointView.MinIdleTime, __instance.WaypointView.MaxIdleTime);
                return false;
            }
            catch (Exception ex)
            {
                Main.GetLogger<RoamingWaypointDataPatches>().LogError(ex, "Unable to select roaming idle time");
                throw;
            }
        }
    }
}
