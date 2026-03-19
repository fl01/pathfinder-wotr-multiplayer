using HarmonyLib;
using Kingmaker.AreaLogic.AlushenyrraIsles;

namespace WOTRMultiplayer.HarmonyPatches.MapObjects
{
    [HarmonyPatch]
    public class IsleStateControllerViewPatches
    {
        [HarmonyPatch(typeof(IslesController), nameof(IslesController.TickCameraRotation))]
        [HarmonyPrefix]
        public static bool IslesController_TickCameraRotation_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.CanControlAlushenyrraIsles();
            return canContinue;
        }
    }
}
