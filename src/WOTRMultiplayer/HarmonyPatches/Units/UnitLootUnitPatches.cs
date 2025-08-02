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
        public static bool UnitLootUnit_OnAction_HarmonyPrefix(UnitLootUnit __instance)
        {
            Main.GetLogger<UnitLootUnitPatches>().LogWarning("----------------LOOT COMMAND");
            return true;
        }
    }
}
