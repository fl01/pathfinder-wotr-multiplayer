using HarmonyLib;
using Kingmaker.Armies.TacticalCombat.Controllers;

namespace WOTRMultiplayer.HarmonyPatches.Combat.Crusades
{
    [HarmonyPatch]
    public class TacticalCombatControllerPatches
    {
        [HarmonyPatch(typeof(TacticalCombatController), nameof(TacticalCombatController.ReportIfFreezed))]
        [HarmonyPrefix]
        public static bool TacticalCombatController_ReportIfFreezed_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            return false;
        }

        [HarmonyPatch(typeof(TacticalCombatController), nameof(TacticalCombatController.ProcessBattleEnd))]
        [HarmonyPrefix]
        public static void TacticalCombatController_ProcessBattleEnd_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnCrusadeArmyCombatEnded();
        }

        [HarmonyPatch(typeof(TacticalCombatController), nameof(TacticalCombatController.Setup))]
        [HarmonyPostfix]
        public static void TacticalCombatController_Setup_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnCrusadeArmyCombatInitialized();
        }
    }
}
