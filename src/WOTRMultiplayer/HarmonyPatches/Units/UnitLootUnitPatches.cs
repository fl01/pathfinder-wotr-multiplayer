using HarmonyLib;
using Kingmaker.UnitLogic.Commands;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches.Units
{
    [HarmonyPatch]
    public class UnitLootUnitPatches
    {
        [HarmonyPatch(typeof(UnitLootUnit), nameof(UnitLootUnit.OnAction))]
        [HarmonyPrefix]
        public static bool UnitLootUnit_OnAction_Prefix(UnitLootUnit __instance)
        {
            Main.GetLogger<UnitLootUnitPatches>().LogCritical("LOOT COMMAND AM I EVER USED?? UnitId={UnitId}, TargetId={TargetId}", __instance.Executor.UniqueId, __instance.Target?.UniqueId);
            return true;
        }
    }
}
