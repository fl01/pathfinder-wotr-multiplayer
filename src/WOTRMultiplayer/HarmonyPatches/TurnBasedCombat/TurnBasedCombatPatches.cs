using HarmonyLib;
using Kingmaker;
using Kingmaker.Controllers.Combat;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Commands.Base;
using Microsoft.Extensions.Logging;
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
        public static bool TurnController_Start_Prefix(TurnController __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            // block on host/client until everyone is not trying to start same turn
            var canContinue = Main.Multiplayer.OnBeforeStartTurn(__instance.Rider.UniqueId);
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

        [HarmonyPatch(typeof(UnitCommand), nameof(UnitCommand.Interrupt))]
        [HarmonyPostfix]
        public static void UnitCommand_Interrupt_Prefix(UnitCommand __instance, bool raiseEvent)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            // is not reliable

            //if (Game.Instance.Player.IsInCombat
            //    && (__instance is UnitMoveTo
            //    || __instance is UnitMoveContiniously
            //    || __instance is UnitMoveAlongPath))
            //{
            //    Main.GetLogger<TurnBasedCombatPatches>().LogInformation("Unit did move. UnitId={unitId}, CharacterName={characterName}", __instance.Executor?.UniqueId, __instance.Executor?.CharacterName);
            //}
        }

        [HarmonyPatch(typeof(UnitCommand), nameof(UnitCommand.OnEnded))]
        [HarmonyPostfix]
        public static void UnitCommand_OnEnded_Prefix(UnitCommand __instance, bool raiseEvent)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            // executed billion times for some reason

            //if (Game.Instance.Player.IsInCombat && __instance is UnitMoveTo moveTo)
            //{
            //    Main.GetLogger<TurnBasedCombatPatches>().LogInformation("Unit move ended. UnitId={unitId}, CharacterName={characterName}", __instance.Executor?.UniqueId, __instance.Executor?.CharacterName);
            //}
        }
    }
}
