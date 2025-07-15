using HarmonyLib;
using Kingmaker.Controllers.Combat;
using Kingmaker.Controllers.Units;
using Kingmaker.UnitLogic.Commands.Base;
using TurnBased.Controllers;

namespace WOTRMultiplayer.HarmonyPatches.TurnBasedCombat
{
    // order after host allowed to start combat
    // UnitCombatPrepareController.Tick -> CombatController_Reset -> CombatController_Tick
    [HarmonyPatch]
    public class TurnBasedCombatPatches
    {
        [HarmonyPatch(typeof(UnitCombatPrepareController), nameof(UnitCombatPrepareController.Tick))]
        [HarmonyPrefix]
        public static bool UnitCombatPrepareController_Tick_Prefix(UnitCombatPrepareController __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.CanTickUnitCombatPrepareController();
            return canContinue;
        }

        [HarmonyPatch(typeof(CombatController), nameof(CombatController.Tick))]
        [HarmonyPrefix]
        public static bool CombatController_Tick_Prefix(CombatController __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.CanTickCombatController();
            return canContinue;
        }

        [HarmonyPatch(typeof(TurnController), nameof(TurnController.Start))]
        [HarmonyPrefix]
        public static bool TurnController_Start_Prefix(TurnController __instance, bool actingInSurpriseRound)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            // block on host/client until everyone is not trying to start same turn
            var canContinue = Main.Multiplayer.OnBeforeStartTurn(__instance.Rider.UniqueId, actingInSurpriseRound);
            return canContinue;
        }

        [HarmonyPatch(typeof(TurnController), nameof(TurnController.End))]
        [HarmonyPrefix]
        public static bool TurnController_End_Prefix(TurnController __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.OnBeforeEndTurn(__instance.Rider.UniqueId);
            return canContinue;
        }

        [HarmonyPatch(typeof(UnitCommandController), nameof(UnitCommandController.TickCommand))]
        [HarmonyPrefix]
        public static bool UnitCommandController_TickCommand_Prefix(UnitCommandController __instance, UnitCommand command, bool canMoveUnit)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            // should be always false for a client + should be sent to host to make that action first

            //if (!Game.Instance.Player.IsInCombat || !command.Executor.IsPlayerFaction)
            //{
            //    return true;
            //}

            //command.Interrupt(false);
            //Main.GetLogger<TurnBasedCombatPatches>().LogInformation("Interrupted command. Type={type}, UnitId={unitId}", command.GetType().Name, command.Executor.UniqueId);
            //return false;
            return true;
        }
    }
}
