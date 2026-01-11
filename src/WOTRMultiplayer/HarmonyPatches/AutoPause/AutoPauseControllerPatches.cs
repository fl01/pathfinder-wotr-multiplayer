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

        /// <summary>
        /// this type of pause is disabled anyway, no need to check if setting is enabled
        /// </summary>
        /// <returns></returns>
        [HarmonyPatch(typeof(AutoPauseController), nameof(AutoPauseController.OnApplicationFocusChanged))]
        [HarmonyPrefix]
        public static bool AutoPauseController_OnApplicationFocusChanged_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            return false;
        }
    }
}
