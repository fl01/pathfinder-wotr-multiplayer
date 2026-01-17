using HarmonyLib;
using Kingmaker.Armies.TacticalCombat.Controllers;

namespace WOTRMultiplayer.HarmonyPatches.Combat.Crusades
{
    [HarmonyPatch]
    public class TacticalCombatInitializationControllerPatches
    {
        [HarmonyPatch(typeof(TacticalCombatInitializationController), nameof(TacticalCombatInitializationController.Activate))]
        [HarmonyPrefix]
        public static bool TacticalCombatInitializationController_Activate_Prefix(TacticalCombatInitializationController __instance)
        {
            if (!Main.Multiplayer.IsActive || !__instance.m_NeedToSetup)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.OnCrusadeArmyCombatInitialization();
            return canContinue;
        }
    }
}
