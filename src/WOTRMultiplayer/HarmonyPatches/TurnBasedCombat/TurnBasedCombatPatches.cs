using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.AI;
using Kingmaker.Controllers.Combat;
using Kingmaker.EntitySystem.Entities;
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
                // but in reality those characters are controlled by other players and we are waiting their actions
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
            var attr = MethodBase.GetCurrentMethod().GetCustomAttribute<HarmonyPatch>();
            var target = $"{attr.info.declaringType.Name}.{attr.info.methodName}";
            var matcher = new CodeMatcher(instructions);
            var lookFor = AccessTools.PropertyGetter(typeof(UnitCombatState), nameof(UnitCombatState.InitiativeRandom));
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match != null)
            {
                var actualValidPosition = matcher.Advance(-1);
                actualValidPosition.RemoveInstructions(matcher.Length - actualValidPosition.Pos - 1); // keep last `ret`
                var newInstructions = new List<CodeInstruction>()
                {
                    // OpCodes.Ldloc_0 is already loaded (UnitEntityData xi)
                    new(OpCodes.Ldloc_1), // (UnitEntityData yi)
                    new(OpCodes.Call, AccessTools.Method(typeof(TurnBasedCombatPatches), nameof(TurnBasedCombatPatches.CompareUnitsByUniqueId)))
                };

                actualValidPosition.Insert(newInstructions);
                Main.GetLogger<HarmonyTranspiler>().LogInformation("Transpiler has been applied. Target={target}", target);
            }

            return matcher.Instructions();
        }

        public static int CompareUnitsByUniqueId(UnitEntityData xi, UnitEntityData yi)
        {
            return xi.UniqueId.CompareTo(yi.UniqueId);
        }

        /// <summary>
        /// skips predictions if !IsDirectlyControllable
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        [HarmonyPatch(typeof(TurnController), nameof(TurnController.UpdateActionPredictions))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TurnController_UpdateActionPredictions_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var attr = MethodBase.GetCurrentMethod().GetCustomAttribute<HarmonyPatch>();
            var target = $"{attr.info.declaringType.Name}.{attr.info.methodName}";
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
            var attr = MethodBase.GetCurrentMethod().GetCustomAttribute<HarmonyPatch>();
            var target = $"{attr.info.declaringType.Name}.{attr.info.methodName}";
            var matcher = new CodeMatcher(instructions);

            ReplaceIsDirectlyControllable(matcher, target);

            return matcher.Instructions();
        }

        private static void ReplaceIsDirectlyControllable(CodeMatcher matcher, string target)
        {
            var replaceWith = AccessTools.Method(typeof(TurnBasedCombatPatches), nameof(TurnBasedCombatPatches.IsControlledByPlayers));
            var lookFor = AccessTools.PropertyGetter(typeof(UnitEntityData), nameof(UnitEntityData.IsDirectlyControllable));
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match != null)
            {
                var call = new CodeInstruction(OpCodes.Call, replaceWith);
                match.RemoveInstruction();
                match.Insert(call);
                Main.GetLogger<HarmonyTranspiler>().LogInformation("Transpiler has been applied. Target={target}", target);
                return;
            }

            Main.GetLogger<HarmonyTranspiler>().LogError("Transpiler has not been applied. Target={target}", target);
        }

        public static bool IsControlledByPlayers(UnitEntityData unit)
        {
            return unit.IsDirectlyControllable ||
                Main.Multiplayer.IsActive && Main.Multiplayer.IsControlledByPlayers(unit.UniqueId);
        }
    }
}
