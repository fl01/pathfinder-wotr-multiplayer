using System;
using HarmonyLib;
using Kingmaker.Controllers;
using Kingmaker.View.Roaming;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Services.Random;

namespace WOTRMultiplayer.HarmonyPatches.Roaming
{
    [HarmonyPatch]
    public class RoamingWaypointDataPatches
    {
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
                var maxRange = __instance.WaypointView.NextWaypoints.Count;
                if (maxRange == 0)
                {
                    return false;
                }

                var uniqueId = $"{__instance.UniqueId}:{nameof(RoamingWaypointData.SelectNextPoint)}";
                int nextWaypointIndex = Main.Multiplayer.ValueGenerator.Range(SeedLifetime.Area, uniqueId, 0, maxRange);
                NextWaypointEntry nextWaypointEntry = __instance.WaypointView.NextWaypoints[nextWaypointIndex];
                __result = nextWaypointEntry?.Waypoint?.WaypointData;

                Main.GetLogger<RoamingWaypointDataPatches>().LogDebug("Selected waypoint. Id={Id}, Position={Position}", uniqueId, __result?.Position);
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
                var uniqueId = $"{__instance.UniqueId}:{nameof(RoamingWaypointData.SelectIdleTime)}";
                float idleTime = Main.Multiplayer.ValueGenerator.Range(SeedLifetime.Area, uniqueId, __instance.WaypointView.MinIdleTime, __instance.WaypointView.MaxIdleTime);
                __result = idleTime.Seconds();

                Main.GetLogger<RoamingWaypointDataPatches>().LogDebug("Selected idle time. Id={Id}, RawTime={RawTime}, Time={Time}, MinTimeRange={MinTimeRange}, MaxTimeRange={MaxTimeRange}", uniqueId, idleTime, __result, __instance.WaypointView.MinIdleTime, __instance.WaypointView.MaxIdleTime);
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
