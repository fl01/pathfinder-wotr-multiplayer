using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Controllers;
using Kingmaker.Controllers.Combat;
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

        [HarmonyPatch(typeof(UnitCombatPrepareController), nameof(UnitCombatPrepareController.Tick))]
        [HarmonyPrefix]
        public static bool UnitCombatPrepareController_Tick_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.CanTickUnitCombatPrepareController();
            return canContinue;
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

            // there are multiple selected units while someone else starts surprise combat
            return false;
        }
    }
}
