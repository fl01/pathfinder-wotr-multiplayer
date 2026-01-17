using HarmonyLib;
using Kingmaker.Armies.TacticalCombat.Controllers;

namespace WOTRMultiplayer.HarmonyPatches.Combat.Crusades
{
    [HarmonyPatch]
    public class TacticalCombatTurnControllerPatches
    {
        [HarmonyPatch(typeof(TacticalCombatTurnController), nameof(TacticalCombatTurnController.TryNextTurnOrMorale))]
        [HarmonyPrefix]
        public static bool TacticalCombatTurnController_TryNextTurnOrMorale_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            return true;
        }
    }
}
