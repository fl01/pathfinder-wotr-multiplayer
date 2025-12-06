using HarmonyLib;
using Kingmaker.Controllers;
using Kingmaker.EntitySystem;
using Kingmaker.View.MapObjects.Traps;
using TurnBased.Controllers;

namespace WOTRMultiplayer.HarmonyPatches.AutoPause
{
    [HarmonyPatch]
    public class AutoPauseControllerPatches
    {
        [HarmonyPatch(typeof(AutoPauseController), nameof(AutoPauseController.OnEntityNoticed))]
        [HarmonyPostfix]
        public static void AutoPauseController_OnEntityNoticed_Postfix(StaticEntityData entity)
        {
            if (!Main.Multiplayer.IsActive || entity is not TrapObjectData || CombatController.IsInTurnBasedCombat())
            {
                return;
            }

            Main.Multiplayer.OnAutoPausedByTrapDetection();
        }
    }
}
