using System;
using HarmonyLib;
using Kingmaker.Controllers;
using Kingmaker.View.Roaming;
using Microsoft.Extensions.Logging;

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

            var nextWaypointIndex = Main.Multiplayer.ValueGenerator.Range(Random.SeedLifetime.Area, __instance.UniqueId, 0, __instance.WaypointView.NextWaypoints.Count);
            NextWaypointEntry nextWaypointEntry = __instance.WaypointView.NextWaypoints[nextWaypointIndex];
            __result = nextWaypointEntry?.Waypoint?.WaypointData;

            Main.GetLogger<RoamingWaypointDataPatches>().LogInformation("Selected waypoint. Id={Id}, Position={Position}", __instance.UniqueId, __result?.Position);
            return false;
        }

        [HarmonyPatch(typeof(RoamingWaypointData), nameof(RoamingWaypointData.SelectIdleTime))]
        [HarmonyPrefix]
        public static bool RoamingWaypointData_SelectIdleTime_Prefix(RoamingWaypointData __instance, ref TimeSpan __result)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var uniqueId = __instance.UniqueId + nameof(RoamingWaypointData.SelectIdleTime);
            var idleTime = Main.Multiplayer.ValueGenerator.Range(Random.SeedLifetime.Area, uniqueId, __instance.WaypointView.MinIdleTime, __instance.WaypointView.MaxIdleTime);
            __result = idleTime.Seconds();

            Main.GetLogger<RoamingWaypointDataPatches>().LogInformation("Selected idle time. Id={Id}, RawTime={RawTime}, Time={Time}", __instance.UniqueId, idleTime, __result);
            return false;
        }
    }
}
