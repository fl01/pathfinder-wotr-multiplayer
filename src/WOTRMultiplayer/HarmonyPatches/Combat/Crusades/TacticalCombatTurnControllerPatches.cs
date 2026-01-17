using HarmonyLib;
using Kingmaker;
using Kingmaker.Armies.TacticalCombat.Controllers;
using WOTRMultiplayer.Entities.Combat.Crusades;

namespace WOTRMultiplayer.HarmonyPatches.Combat.Crusades
{
    [HarmonyPatch]
    public class TacticalCombatTurnControllerPatches
    {
        [HarmonyPatch(typeof(TacticalCombatTurnController), nameof(TacticalCombatTurnController.HandleNextTurn))]
        [HarmonyPrefix]
        public static void TacticalCombatTurnController_HandleNextTurn_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var data = Game.Instance.TacticalCombat.Data;
            var turn = new NetworkArmyCombatTurn
            {
                UnitId = data.Turn.Unit?.UniqueId,
                Number = data.Turn.Number,
                IsAI = !data.Turn.Unit?.IsDirectlyControllable ?? false
            };

            Main.Multiplayer.OnCrusadeArmyCombatTurnStarted(turn);
        }

        [HarmonyPatch(typeof(TacticalCombatTurnController), nameof(TacticalCombatTurnController.TryNextTurnOrMorale))]
        [HarmonyPrefix]
        public static bool TacticalCombatTurnController_TryNextTurnOrMorale_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var data = Game.Instance.TacticalCombat.Data;
            var canContinue = Main.Multiplayer.OnBeforeCrusadeArmyCombatTurnStart(data.Turn.Number);
            return canContinue;
        }
    }
}
