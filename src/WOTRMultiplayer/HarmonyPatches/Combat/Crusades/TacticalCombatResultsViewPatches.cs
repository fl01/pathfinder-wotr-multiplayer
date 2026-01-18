using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.TacticalCombat.Result;
using Kingmaker.UI.MVVM._VM.TacticalCombat.Result;

namespace WOTRMultiplayer.HarmonyPatches.Combat.Crusades
{
    [HarmonyPatch]
    public class TacticalCombatResultsViewPatches
    {
        [HarmonyPatch(typeof(TacticalCombatResultsPCView), nameof(TacticalCombatResultsPCView.BindViewImplementation))]
        [HarmonyPostfix]
        public static void TacticalCombatResultsPCView_BindViewImplementation_Postfix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnCrusadeArmyBattleResultsShown();
        }

        [HarmonyPatch(typeof(TacticalCombatResultsVM), nameof(TacticalCombatResultsVM.Close))]
        [HarmonyPrefix]
        public static void TacticalCombatController_Close_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnCrusadeArmyBattleResultsClosed();
        }

        [HarmonyPatch(typeof(TacticalCombatResultsVM), nameof(TacticalCombatResultsVM.StartManualCombat))]
        [HarmonyPrefix]
        public static void TacticalCombatController_StartManualCombat_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnCrusadeArmyBattleResultsManualCombatStarted();
        }
    }
}
