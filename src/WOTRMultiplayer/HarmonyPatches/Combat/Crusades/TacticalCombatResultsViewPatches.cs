using HarmonyLib;
using Kingmaker.Armies.TacticalCombat;
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
        public static void TacticalCombatResultsVM_Close_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var isTacticalCombat = TacticalCombatHelper.IsActive;
            Main.Multiplayer.OnCrusadeArmyBattleResultsClosed(isTacticalCombat);
        }

        [HarmonyPatch(typeof(TacticalCombatResultsVM), nameof(TacticalCombatResultsVM.StartManualCombat))]
        [HarmonyPrefix]
        public static void TacticalCombatResultsVM_StartManualCombat_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnCrusadeArmyBattleResultsManualCombatStarted();
        }
    }
}
