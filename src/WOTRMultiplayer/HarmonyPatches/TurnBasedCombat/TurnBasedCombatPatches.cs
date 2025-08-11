using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.AI;
using Kingmaker.Controllers.Combat;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.TurnBasedMode;
using Microsoft.Extensions.Logging;
using TurnBased.Controllers;

namespace WOTRMultiplayer.HarmonyPatches.TurnBasedCombat
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

        [HarmonyPatch(typeof(TurnController), nameof(TurnController.Start))]
        [HarmonyPrefix]
        public static bool TurnController_Start_Prefix(TurnController __instance, bool actingInSurpriseRound)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

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

        [HarmonyPatch(typeof(AiBrainController), nameof(AiBrainController.TickBrain))]
        [HarmonyPrefix]
        public static bool AiBrainController_TickBrain_Prefix(AiBrainController __instance, UnitEntityData unit)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = !Main.Multiplayer.IsControlledByPlayers(unit.UniqueId);
            if (!canContinue && Game.Instance.TurnBasedCombatController.CurrentTurn != null)
            {
                // game treats characters without control as AI and tries to skip turn if they are stuck
                // but in reality those characters are controlled by other players and we are waiting for their actions
                // I believe this could reworked by using transpiler to replace generic condition 'IsDirectlyControllable' in TurnController.Tick => (Status == TurnStatus.Acting && Rider.Commands.Empty && !Rider.IsDirectlyControllable)
                // with something smarter, but resetting counters work fine for now
                Game.Instance.TurnBasedCombatController.CurrentTurn.AIForcedTickCount = 0;
                Game.Instance.TurnBasedCombatController.CurrentTurn.FramesWaitedForStuckAI = 0;
                Game.Instance.TurnBasedCombatController.CurrentTurn.TimeWaitedForIdleAI = 0;
            }

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
            var result = string.Compare(xi.UniqueId, yi.UniqueId, System.StringComparison.OrdinalIgnoreCase);
            Main.GetLogger<CombatController.UnitsOrderComaprer>().LogInformation("Units have same initiave order, comparing by uniqueId. Result={result}, Unit1={unit1}, Unit2={unit2}", result, xi.UniqueId, yi.UniqueId);
            return result;
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
            if (!SetCallUpdateActionPredictionsIfControlled(instructions, matcher))
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
            if (!SetCallUpdateActionPredictionsIfControlled(instructions, matcher))
            {
                Main.GetLogger<HarmonyTranspiler>().LogError("Transpiler has not been applied. Target={target}", target);
                return instructions;
            }

            Main.GetLogger<TurnBasedCombatPatches>().LogInformation("Transpiler has been applied. Target={target}", target);
            return matcher.Instructions();
        }

        private static bool SetCallUpdateActionPredictionsIfControlled(IEnumerable<CodeInstruction> instructions, CodeMatcher matcher)
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

            ReplaceIsDirectlyControllable(matcher, target);

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

            ReplaceIsDirectlyControllable(matcher, target);

            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(TurnController), nameof(TurnController.OnInterruptCommand))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TurnController_OnInterruptCommand_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);

            ReplaceIsDirectlyControllable(matcher, target, true);
            ReplaceIsDirectlyControllable(matcher, target, true);

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

            ReplaceIsDirectlyControllable(matcher, target);

            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(TurnController), nameof(TurnController.TrySelectMovementLimit))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TurnController_TrySelectMovementLimit_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);

            ReplaceIsDirectlyControllable(matcher, target);

            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(TurnController), nameof(TurnController.HasExtraAction))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TurnController_HasExtraAction_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);

            ReplaceIsDirectlyControllable(matcher, target);

            return matcher.Instructions();
        }

        public static void TryCatchedInvokeCursorUpdate(TurnController turnController)
        {
            try
            {
                turnController.InvokeCursorUpdate();
            }
            catch (System.Exception)
            {
                // who cares about cursor, but please, stop spamming errors
            }
        }

        private static void ReplaceIsDirectlyControllable(CodeMatcher matcher, string target, bool withLabels = false, bool fromEnd = false)
        {
            var replaceWith = AccessTools.Method(typeof(TurnBasedCombatPatches), nameof(TurnBasedCombatPatches.IsControlledByPlayers));
            var lookFor = AccessTools.PropertyGetter(typeof(UnitEntityData), nameof(UnitEntityData.IsDirectlyControllable));
            var match = fromEnd ? matcher.End().SearchBackwards(x => x.Calls(lookFor)) : matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<TurnBasedCombatPatches>().LogError("Transpiler has not been applied. Target={target}", target);
                return;
            }

            var call = new CodeInstruction(OpCodes.Call, replaceWith);
            if (withLabels)
            {
                var labels = match.Instruction.ExtractLabels();
                call = call.WithLabels(labels);
            }

            match.RemoveInstruction();
            match.Insert(call);

            Main.GetLogger<TurnBasedCombatPatches>().LogInformation("Transpiler has been applied. Target={target}", target);
        }

        public static bool IsControlledByPlayers(UnitEntityData unit)
        {
            try
            {
                return unit.IsDirectlyControllable || Main.Multiplayer.IsActive && Main.Multiplayer.IsControlledByPlayers(unit.UniqueId);
            }
            catch (System.Exception ex)
            {
                Main.GetLogger<TurnBasedCombatPatches>().LogError(ex, "Failed to check if controlled by players. UnitId={unitId}", unit?.UniqueId);
                throw;
            }
        }
    }
}
