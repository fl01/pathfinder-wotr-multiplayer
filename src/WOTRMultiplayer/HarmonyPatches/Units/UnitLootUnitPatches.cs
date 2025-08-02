using HarmonyLib;
using Kingmaker.UnitLogic.Commands;
using Microsoft.Extensions.Logging;
using static Kingmaker.UnitLogic.Commands.Base.UnitCommand;

namespace WOTRMultiplayer.HarmonyPatches.Units
{
    [HarmonyPatch]
    public class UnitLootUnitPatches
    {
        [HarmonyPatch(typeof(UnitLootUnit), nameof(UnitLootUnit.OnAction))]
        [HarmonyPrefix]
        public static bool UnitLootUnit_OnAction_HarmonyPrefix(UnitLootUnit __instance, ref ResultType __result)
        {
            Main.GetLogger<UnitLootUnitPatches>().LogWarning("LOOT COMMAND");
            return true;
        }
    }
}
