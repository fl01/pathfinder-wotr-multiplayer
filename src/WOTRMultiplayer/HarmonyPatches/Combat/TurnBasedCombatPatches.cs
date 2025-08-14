using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Controllers.Combat;
using Kingmaker.Controllers.Units;
using Kingmaker.Designers.EventConditionActionSystem.Actions;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Persistence.JsonUtility;
using Kingmaker.TurnBasedMode;
using Microsoft.Extensions.Logging;
using TurnBased.Controllers;

namespace WOTRMultiplayer.HarmonyPatches.Combat
{
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

        [HarmonyPatch(typeof(CombatController), nameof(CombatController.StartTurn))]
        [HarmonyPrefix]
        public static bool CombatController_StartTurn_Prefix(CombatController __instance, UnitEntityData unit)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var unitInfo = __instance.FindUnitInfo(unit);
            if (unitInfo == null)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.OnBeforeStartTurn(unit.UniqueId, unitInfo.ActingInSurpriseRound);
            if (!canContinue)
            {
                // creating fake turn to restrict rechoosing unit / starting new turn before all the confirmations
                __instance.CurrentTurn = new TurnController((JsonConstructorMark)default);
            }

            return canContinue;
        }


        [HarmonyPatch(typeof(TurnController), nameof(TurnController.Tick))]
        [HarmonyPrefix]
        public static bool TurnController_Tick_Prefix(TurnController __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            return __instance.Rider != null;
        }

        [HarmonyPatch(typeof(TurnController), nameof(TurnController.Start))]
        [HarmonyPrefix]
        public static bool TurnController_Start_Prefix(TurnController __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            return __instance.Rider != null;
        }

        [HarmonyPatch(typeof(TurnController), nameof(TurnController.TrySelectDirectControllableUnit))]
        [HarmonyPrefix]
        public static bool TurnController_TrySelectDirectControllableUnit_Prefix(TurnController __instance, bool checkIfSelected)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            TurnControllerTrySelectDirectControllableUnit(__instance, checkIfSelected);
            return false;
        }

        private static void TurnControllerTrySelectDirectControllableUnit(TurnController __instance, bool checkIfSelected)
        {
            try
            {
                UnitEntityData unitEntityData = null;
                if (__instance.Rider.IsDirectlyControllable)
                {
                    unitEntityData = __instance.Rider;
                }
                else
                {
                    UnitEntityData mount = __instance.Mount;
                    if (mount != null && mount.IsDirectlyControllable)
                    {
                        unitEntityData = __instance.Mount;
                    }
                }
                if (unitEntityData == null)
                {
                    return;
                }
                if (checkIfSelected)
                {
                    UnitEntityData singleSelectedUnit = Game.Instance.SelectionCharacter.SingleSelectedUnit;
                    if (singleSelectedUnit != null && singleSelectedUnit.IsCurrentUnit())
                    {
                        return;
                    }
                }
                Game.Instance.SelectionCharacter.SetSelected(unitEntityData);
            }
            catch (Exception ex)
            {
                Main.GetLogger<TurnBasedCombatPatches>().LogError(ex, "TrySelectDirectControllableUnit");
                throw;
            }
        }

        [HarmonyPatch(typeof(TurnController), nameof(TurnController.TrySelectDirectControllableUnit))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TurnController_TrySelectDirectControllableUnit_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);

            CommonTranspilerReplacements.ReplaceIsDirectlyControllable(matcher, target);
            CommonTranspilerReplacements.ReplaceIsDirectlyControllable(matcher, target, true);

            return matcher.Instructions();
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

        /// <summary>
        /// The game relies on a random number to determine turn order in cases where Initiative/Stats are the same. Unfortunately, this leads to a possible (50%) desync between MP clients
        /// This transpiler modifies the comparer to stop relying on CombatState.InitiativeRandom and instead compare UnitEntityData.UniqueId which produces same results on different PCs
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        [HarmonyPatch(typeof(CombatController.UnitsOrderComaprer), nameof(CombatController.UnitsOrderComaprer.Compare))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> UnitsOrderComaprer_Compare_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            var lookFor = AccessTools.PropertyGetter(typeof(UnitCombatState), nameof(UnitCombatState.InitiativeRandom));
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<TurnBasedCombatPatches>().LogError("Transpiler has not been applied. Target={target}", target);
                return matcher.Instructions();
            }

            var actualValidPosition = matcher.Advance(-1);
            actualValidPosition.RemoveInstructions(matcher.Length - actualValidPosition.Pos - 1); // keep last `ret`
            var newInstructions = new List<CodeInstruction>()
            {
                // OpCodes.Ldloc_0 is already loaded (UnitEntityData xi)
                new(OpCodes.Ldloc_1), // (UnitEntityData yi)
                new(OpCodes.Call, AccessTools.Method(typeof(TurnBasedCombatPatches), nameof(TurnBasedCombatPatches.CompareUnitsByUniqueId)))
            };
            actualValidPosition.Insert(newInstructions);
            Main.GetLogger<TurnBasedCombatPatches>().LogInformation("Transpiler has been applied. Target={target}", target);

            return matcher.Instructions();
        }

        /// <summary>
        /// Removes random initiative
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        [HarmonyPatch(typeof(UnitCombatPrepareController), nameof(UnitCombatPrepareController.Tick))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> UnitCombatPrepareController_Tick_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            var lookFor = AccessTools.PropertySetter(typeof(UnitCombatState), nameof(UnitCombatState.InitiativeRandom));
            CodeMatcher match;
            int replacementCounter = 0;
            while ((match = matcher.SearchForward(x => x.Calls(lookFor))).IsValid)
            {
                match = match.Advance(-8);
                match.RemoveInstructions(9);
                var newInstructions = new List<CodeInstruction>
                {
                    new (OpCodes.Ldc_I4_1)
                };
                match.Insert(newInstructions);
                replacementCounter++;
            }

            const int ExpectedReplacementCounter = 2;
            if (replacementCounter != ExpectedReplacementCounter)
            {
                Main.GetLogger<TurnBasedCombatPatches>().LogError("Instructions have not been replaced expected number of times. Target={target}, Expected={expected}, Current={current}", target, ExpectedReplacementCounter, replacementCounter);
                return instructions;
            }

            Main.GetLogger<TurnBasedCombatPatches>().LogInformation("Transpiler has been applied. Target={target}", target);
            return matcher.Instructions();
        }

        public static int CompareUnitsByUniqueId(UnitEntityData xi, UnitEntityData yi)
        {
            try
            {
                var result = new[] { xi, yi }.OrderBy(x => x.UniqueId, StringComparer.OrdinalIgnoreCase).First() == xi ? -1 : 1;
                Main.GetLogger<CombatController.UnitsOrderComaprer>().LogInformation("Units have same initiave order, comparing by uniqueId. Result={result}, Unit1={unit1}, Unit2={unit2}", result, xi.UniqueId, yi.UniqueId);
                return result;
            }
            catch (Exception ex)
            {
                Main.GetLogger<CombatController.UnitsOrderComaprer>().LogError(ex, "Error while comparing by unique id. Unit1={unit1}, Unit2={unit2}", xi.UniqueId, yi.UniqueId);
                throw;
            }
        }

        /// <summary>
        /// Should cancel player commands (if IsDirectlyControllable)
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        [HarmonyPatch(typeof(TurnBasedCommandCancelController), nameof(TurnBasedCommandCancelController.HandleUnitMakeOffensiveAction))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TurnBasedCommandCancelController_HandleUnitMakeOffensiveAction_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);

            CommonTranspilerReplacements.ReplaceIsDirectlyControllable(matcher, target);
            CommonTranspilerReplacements.ReplaceIsDirectlyControllable(matcher, target);

            return matcher.Instructions();
        }

        /// <summary>
        /// FullAttack is always true if !IsDirectlyControllable
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        [HarmonyPatch(typeof(TurnController), nameof(TurnController.GetEnabledFullAttack))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TurnController_GetEnabledFullAttack_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);

            CommonTranspilerReplacements.ReplaceIsDirectlyControllable(matcher, target);

            return matcher.Instructions();
        }

        ///// <summary>
        ///// skips predictions if !IsDirectlyControllable
        ///// </summary>
        ///// <param name="instructions"></param>
        ///// <returns></returns>
        //[HarmonyPatch(typeof(TurnController), nameof(TurnController.UpdateActionPredictions))]
        //[HarmonyTranspiler]
        //public static IEnumerable<CodeInstruction> TurnController_UpdateActionPredictions_Transpiler(IEnumerable<CodeInstruction> instructions)
        //{
        //    var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
        //    var matcher = new CodeMatcher(instructions);

        //    ReplaceIsDirectlyControllable(matcher, target);

        //    return matcher.Instructions();
        //}

        /// <summary>
        /// skips updating predictions if you have no controll over character
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        [HarmonyPatch(typeof(TurnController), nameof(TurnController.HandlePortraitHover))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TurnController_HandlePortraitHover_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            if (!SetCallUpdateActionPredictionsIfControlled(matcher))
            {
                Main.GetLogger<TurnBasedCombatPatches>().LogError("Transpiler has not been applied. Target={target}", target);
                return instructions;
            }

            Main.GetLogger<TurnBasedCombatPatches>().LogInformation("Transpiler has been applied. Target={target}", target);
            return matcher.Instructions();
        }

        // <summary>
        /// skips updating predictions if you have no controll over character
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        [HarmonyPatch(typeof(TurnController), nameof(TurnController.HandleOvertipHover))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TurnController_HandleOvertipHover_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            if (!SetCallUpdateActionPredictionsIfControlled(matcher))
            {
                Main.GetLogger<HarmonyTranspiler>().LogError("Transpiler has not been applied. Target={target}", target);
                return instructions;
            }

            Main.GetLogger<TurnBasedCombatPatches>().LogInformation("Transpiler has been applied. Target={target}", target);
            return matcher.Instructions();
        }

        private static bool SetCallUpdateActionPredictionsIfControlled(CodeMatcher matcher)
        {
            var replaceWith = AccessTools.Method(typeof(TurnBasedCombatPatches), nameof(TurnBasedCombatPatches.UpdateActionPredictionsIfControlled));
            var lookFor = AccessTools.Method(typeof(TurnController), nameof(TurnController.UpdateActionPredictions));
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            if (!match.IsValid)
            {
                return false;
            }

            match = match
                .Advance(-1)
                .RemoveInstructions(2);

            var newInstructions = new List<CodeInstruction>
            {
                new(OpCodes.Call, replaceWith)
            };
            match.Insert(newInstructions);

            return true;
        }

        public static void UpdateActionPredictionsIfControlled()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var currentUnit = Game.Instance.TurnBasedCombatController.CurrentTurn?.Rider;
            if (currentUnit != null && Main.Multiplayer.IsControlledByLocalPlayer(currentUnit.UniqueId))
            {
                Game.Instance.TurnBasedCombatController.CurrentTurn.UpdateActionPredictions();
            }
        }

        /// <summary>
        /// IsDirectlyControllable must be true to avoid gettin up automatically
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        [HarmonyPatch(typeof(UnitProneController), nameof(UnitProneController.Tick))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> UnitProneController_Tick_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);

            CommonTranspilerReplacements.ReplaceIsDirectlyControllable(matcher, target);

            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(StartCombat), nameof(StartCombat.RunAction))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> StartCombat_RunAction_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);

            CommonTranspilerReplacements.ReplaceIsDirectlyControllable(matcher, target);
            CommonTranspilerReplacements.ReplaceIsDirectlyControllable(matcher, target);
            CommonTranspilerReplacements.ReplaceIsDirectlyControllable(matcher, target);

            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(UnitEntityData), nameof(UnitEntityData.AlwaysRevealedInFogOfWar), MethodType.Getter)]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> UnitEntityData_AlwaysRevealedInFogOfWar_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);

            CommonTranspilerReplacements.ReplaceIsDirectlyControllable(matcher, target);

            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(SceneEntitiesStateConverter), nameof(SceneEntitiesStateConverter.CanBeOptimizedUnit))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> SceneEntitiesStateConverter_CanBeOptimizedUnit_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);

            CommonTranspilerReplacements.ReplaceIsDirectlyControllable(matcher, target);

            return matcher.Instructions();
        }

        // some replacement breaks touch abilities with move part
        //[HarmonyPatch(typeof(TurnController), nameof(TurnController.Prepare))]
        //[HarmonyTranspiler]
        //public static IEnumerable<CodeInstruction> TurnController_Prepare_Transpiler(IEnumerable<CodeInstruction> instructions)
        //{
        //    var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
        //    var matcher = new CodeMatcher(instructions);

        //    for (int i = 1; i <= 7; i++)
        //    {
        //        ReplaceIsDirectlyControllable(matcher, target, i == 7);
        //    }

        //    return matcher.Instructions();
        //}

        /// <summary>
        /// Resets path each frame if !IsDirectlyControllable which breaks Multi Command sticky touch abilities, e.g. Cure Wounds
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        [HarmonyPatch(typeof(PathVisualizer), nameof(PathVisualizer.Update))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> PathVisualizer_Update_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);

            CommonTranspilerReplacements.ReplaceIsDirectlyControllable(matcher, target);

            return matcher.Instructions();
        }

        /// <summary>
        /// IsDirectlyControllable must be true to set UnitCanGetUpOnCommand
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        [HarmonyPatch(typeof(TurnController), nameof(TurnController.Start))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TurnController_Start_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);

            CommonTranspilerReplacements.ReplaceIsDirectlyControllable(matcher, target);

            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(TurnController), nameof(TurnController.OnInterruptCommand))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TurnController_OnInterruptCommand_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);

            CommonTranspilerReplacements.ReplaceIsDirectlyControllable(matcher, target, true);
            CommonTranspilerReplacements.ReplaceIsDirectlyControllable(matcher, target, true);

            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(TurnController), nameof(TurnController.Tick))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TurnController_Tick_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);

            var replaceWith = AccessTools.Method(typeof(TurnBasedCombatPatches), nameof(TurnBasedCombatPatches.TryCatchedInvokeCursorUpdate));
            var lookFor = AccessTools.Method(typeof(TurnController), nameof(TurnController.InvokeCursorUpdate));
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<TurnBasedCombatPatches>().LogError("Transpiler has not been applied. Target={target}", target);
                return matcher.Instructions();
            }

            var call = new CodeInstruction(OpCodes.Call, replaceWith);

            match.RemoveInstruction();
            match.Insert(call);

            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(TurnController), nameof(TurnController.ContinueActing))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TurnController_ContinueActing_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);

            CommonTranspilerReplacements.ReplaceIsDirectlyControllable(matcher, target);

            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(TurnController), nameof(TurnController.TrySelectMovementLimit))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TurnController_TrySelectMovementLimit_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);

            CommonTranspilerReplacements.ReplaceIsDirectlyControllable(matcher, target);

            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(TurnController), nameof(TurnController.HasExtraAction))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TurnController_HasExtraAction_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);

            CommonTranspilerReplacements.ReplaceIsDirectlyControllable(matcher, target);

            return matcher.Instructions();
        }

        public static void TryCatchedInvokeCursorUpdate(TurnController turnController)
        {
            try
            {
                turnController.InvokeCursorUpdate();
            }
            catch (Exception)
            {
                // who cares about cursor, but please, stop spamming errors
            }
        }
    }
}
