using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Controllers;
using Kingmaker.Controllers.Combat;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Persistence.JsonUtility;
using Kingmaker.TurnBasedMode;
using Kingmaker.UI;
using Kingmaker.UI._ConsoleUI.TurnBasedMode;
using Microsoft.Extensions.Logging;
using TurnBased.Controllers;

namespace WOTRMultiplayer.HarmonyPatches.Combat
{
    [HarmonyPatch]
    public class TurnBasedCombatPatches
    {
        [HarmonyPatch(typeof(PlayerUISettings), nameof(PlayerUISettings.DoSpeedUp))]
        [HarmonyPrefix]
        public static bool PlayerUISettings_DoSpeedUp_Prefix()
        {
            return !Main.Multiplayer.IsActive;
        }

        [HarmonyPatch(typeof(InitiativeTrackerVM), nameof(InitiativeTrackerVM.InterruptMovement))]
        [HarmonyPrefix]
        public static bool InitiativeTrackerVM_InterruptMovement_Prefix()
        {
            return !Main.Multiplayer.IsActive;
        }

        [HarmonyPatch(typeof(TurnController), nameof(TurnController.TryScrollToUnit))]
        [HarmonyPrefix]
        public static bool TurnController_TryScrollToUnit_Prefix(TurnController __instance, ref bool __result)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            // fails with NRE anyway
            if (__instance.Rider == null)
            {
                __result = false;
                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(CombatController), nameof(CombatController.HandleCombatStart))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> CombatController_HandleCombatStart_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(TurnBasedCombatPatches), nameof(TurnBasedCombatPatches.HasMoreThanOneSelectedUnit));
            var lookFor = AccessTools.PropertyGetter(typeof(SelectionCharacterController), nameof(SelectionCharacterController.SelectedUnits));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<TurnBasedCombatPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Call, replaceWith),
            };
            match = match.Advance(-2).RemoveInstructions(6).Insert(newInstructions);

            Main.GetLogger<TurnBasedCombatPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
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

        [HarmonyPatch(typeof(TurnController), nameof(TurnController.CanEndTurn))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TurnController_CanEndTurn_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);

            CommonTranspilerReplacements.ReplaceIsDirectlyControllableWithLocalPlayerCheck(matcher, target, true);

            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(TurnController), nameof(TurnController.CanDelay))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TurnController_CanDelay_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);

            CommonTranspilerReplacements.ReplaceIsDirectlyControllableWithLocalPlayerCheck(matcher, target, true);

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
                if (unitInfo == null)
                {
                    Main.GetLogger<TurnBasedCombatPatches>().LogWarning("Requested unit has no combat info. UnitId={UnitId}", unit.UniqueId);
                    return true;
                }

                var canContinue = Main.Multiplayer.OnBeforeTurnStart(unit.UniqueId, unitInfo.ActingInSurpriseRound);
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
                Main.GetLogger<TurnBasedCombatPatches>().LogError(ex, "Error while starting combat turn");
                throw;
            }
        }

        [HarmonyPatch(typeof(TurnController), nameof(TurnController.Tick))]
        [HarmonyPrefix]
        public static bool TurnController_Tick_Prefix(TurnController __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            if (!Main.Multiplayer.IsSourceOfAIActions())
            {
                __instance.FramesWaitedForStuckAI = 0;
                __instance.TimeWaitedForIdleAI = 0;
                __instance.TimeWaitedToEndTurn = 0;
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

        [HarmonyPatch(typeof(TurnController), nameof(TurnController.End))]
        [HarmonyPrefix]
        public static bool TurnController_End_Prefix(TurnController __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.OnBeforeTurnEnd(__instance.Rider?.UniqueId);
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
                Main.GetLogger<TurnBasedCombatPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
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
            Main.GetLogger<TurnBasedCombatPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);

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
                Main.GetLogger<TurnBasedCombatPatches>().LogError("Instructions have not been replaced expected number of times. Target={Target}, Expected={expected}, Current={current}", target, ExpectedReplacementCounter, replacementCounter);
                return instructions;
            }

            Main.GetLogger<TurnBasedCombatPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
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

        [HarmonyPatch(typeof(TurnController), nameof(TurnController.HandlePortraitHover))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TurnController_HandlePortraitHover_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            if (!SetCallUpdateActionPredictionsIfControlled(matcher))
            {
                Main.GetLogger<TurnBasedCombatPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            Main.GetLogger<TurnBasedCombatPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(TurnController), nameof(TurnController.HandleOvertipHover))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TurnController_HandleOvertipHover_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            if (!SetCallUpdateActionPredictionsIfControlled(matcher))
            {
                Main.GetLogger<HarmonyTranspiler>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            Main.GetLogger<TurnBasedCombatPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
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

        [HarmonyPatch(typeof(PathVisualizer), nameof(PathVisualizer.Update))]
        [HarmonyPrefix]
        public static bool PathVisualizer_Update_Prefix(PathVisualizer __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var rider = Game.Instance.TurnBasedCombatController.CurrentTurn?.Rider;
            if (Main.Multiplayer.IsControlledByLocalPlayer(rider?.UniqueId))
            {
                return true;
            }

            // casting sticky touch ability (e.g. Cure Wounds) actually creates x2 ability usages.
            // Although we set ForcedPath for the first command, it's not propagated directly to the second one.
            // Second command relies on PathVisualizer.Instance.m_currentPath value to move caster to target in combat
            // so it must not be cleared while we are casting original ability. And that's the reason why we can't enable path visualizer for players who don't own the current turn, as it will corrupt path on any update (e.g. mouse movement)
            if (rider?.Commands.UnitUseAbility == null && rider?.Commands.Attack == null)
            {
                __instance.Clear();
            }

            return false;
        }

        [HarmonyPatch(typeof(PathVisualizer), nameof(PathVisualizer.SetGradientsToRenderer))]
        [HarmonyPrefix]
        public static bool PathVisualizer_SetGradientsToRenderer_Prefix(PathVisualizer __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            // it fails with NRE anyway, just less error logs
            return __instance.m_VisualPath.Any();
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
    }
}
