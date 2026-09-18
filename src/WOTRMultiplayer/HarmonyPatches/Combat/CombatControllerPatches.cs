using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Controllers;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Persistence.JsonUtility;
using Microsoft.Extensions.Logging;
using TurnBased.Controllers;

namespace WOTRMultiplayer.HarmonyPatches.Combat
{
    [HarmonyPatch]
    public class CombatControllerPatches
    {
        [HarmonyPatch(typeof(CombatController), nameof(CombatController.HandleCombatStart))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> CombatController_HandleCombatStart_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(CombatControllerPatches), nameof(CombatControllerPatches.HasMoreThanOneSelectedUnit));
            var lookFor = AccessTools.PropertyGetter(typeof(SelectionCharacterController), nameof(SelectionCharacterController.SelectedUnits));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<CombatControllerPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Call, replaceWith),
            };
            match = match.Advance(-2).RemoveInstructions(6).Insert(newInstructions);

            Main.GetLogger<CombatControllerPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(CombatController), nameof(CombatController.HandleDelayTurn))]
        [HarmonyPrefix]
        public static void CombatController_HandleDelayTurn_Prefix(UnitEntityData unit, UnitEntityData targetUnit)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnHandleDelayCombatTurn(unit.UniqueId, targetUnit.UniqueId);
        }

        [HarmonyPatch(typeof(CombatController), nameof(CombatController.Tick))]
        [HarmonyPrefix]
        public static bool CombatController_Tick_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.CanTickCombatController();
            return canContinue;
        }

        [HarmonyPatch(typeof(CombatController), nameof(CombatController.StartTurn))]
        [HarmonyPrefix]
        public static bool CombatController_StartTurn_Prefix(CombatController __instance, UnitEntityData unit)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            try
            {
                var unitInfo = __instance.FindUnitInfo(unit);
                var canContinue = Main.Multiplayer.OnBeforeTurnStart(unit.UniqueId, unitInfo?.ActingInSurpriseRound ?? false);
                if (!canContinue)
                {
                    // creating fake turn to restrict rechoosing unit / starting new turn before all the confirmations
                    __instance.CurrentTurn = new TurnController((JsonConstructorMark)default);
                    __instance.TurnStartTime = Game.Instance.TimeController.GameTime;
                }

                return canContinue;
            }
            catch (Exception ex)
            {
                Main.GetLogger<CombatControllerPatches>().LogError(ex, "Error while starting combat turn");
                throw;
            }
        }

        [HarmonyPatch(typeof(CombatController), nameof(CombatController.UpdateNavigationGridTags))]
        [HarmonyPrefix]
        public static bool CombatController_UpdateNavigationGridTags_Prefix(CombatController __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            // it fails with NRE anyway, just less error logs
            return AstarPath.active?.data?.gridGraph != null && __instance.CurrentTurn?.Rider != null;
        }

        private static bool HasMoreThanOneSelectedUnit()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return Game.Instance.SelectionCharacter.SelectedUnits.Count > 1;
            }

            // base game has a bug with charge + surprise round + selected unit count
            // pause -> charge -> select two+ more units -> unpause -> your unit will start charging, but the charge command will be interrupted at the start of combat + your turn will be skipped
            // returning true from this method will actually repeat base game behavior + bug, so we need to make sure to fix this condition only if some unit is charging
            var isAnyUnitCharging = Game.Instance.Player.Party.Any(p => p.State.IsCharging);
            return !isAnyUnitCharging;
        }
    }
}
