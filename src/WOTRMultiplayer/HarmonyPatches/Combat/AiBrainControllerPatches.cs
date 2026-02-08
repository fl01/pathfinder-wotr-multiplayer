using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.AI;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Pathfinding;
using Kingmaker.RuleSystem;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Commands.Base;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.Services.Random;

namespace WOTRMultiplayer.HarmonyPatches.Combat
{
    [HarmonyPatch]
    public class AiBrainControllerPatches
    {
        [HarmonyPatch(typeof(CombatAiData), nameof(CombatAiData.UseAction))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> CombatAiData_UseAction_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            var lookFor = AccessTools.Method(typeof(RulebookEvent.Dice), nameof(RulebookEvent.Dice.D), [typeof(DiceFormula)]);
            var replaceWith = AccessTools.Method(typeof(AiBrainControllerPatches), nameof(AiBrainControllerPatches.RollAIActionCooldown));
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<AiBrainControllerPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_1),
                new(OpCodes.Ldarg_2),
                new(OpCodes.Call, replaceWith)
            };
            match = match.RemoveInstruction().Insert(newInstructions);
            Main.GetLogger<AiBrainControllerPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);

            return matcher.Instructions();
        }

        private static int RollAIActionCooldown(DiceFormula diceFormula, AiAction aiAction, UnitCommand unitCommand)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return RulebookEvent.Dice.D(diceFormula);
            }

            try
            {
                var sessionSeed = Main.Multiplayer.GetSessionSeed();
                var combatSeed = Main.Multiplayer.GetCombatSeed();
                var combatTurnSeed = Main.Multiplayer.GetCombatTurnSeed();
                var identifier = $"{nameof(CombatAiData)}:{nameof(CombatAiData.UseAction)}:{nameof(RollAIActionCooldown)}:{Game.Instance.Player.GameId}:{unitCommand.Executor.UniqueId}:{aiAction.Blueprint.AssetGuid}:{aiAction.Blueprint.name}:{unitCommand.TargetUnit?.UniqueId}:{sessionSeed}:{combatSeed}:{combatTurnSeed}";
                var random = Main.Multiplayer.ValueGenerator.GetRandom(SeedLifetime.CombatTurn, identifier);
                var cooldown = diceFormula.Roll(random);
                Main.GetLogger<AiBrainControllerPatches>().LogInformation("AI action cooldown has been rolled. Cooldown={Cooldown}, Identifier={Identifier}", cooldown, identifier);
                return cooldown;
            }
            catch (Exception ex)
            {
                Main.GetLogger<AiBrainControllerPatches>().LogError(ex, "Unable to roll AI action cooldown. UnitId={UnitId}", unitCommand.Executor?.UniqueId);
                throw;
            }
        }

        [HarmonyPatch(typeof(AiBrainController), nameof(AiBrainController.FindBestAction))]
        [HarmonyPostfix]
        public static void AiBrainController_FindBestAction_Postfix(UnitEntityData unit, DecisionContext context, ref AiAction bestActionResult, ref UnitEntityData bestTargetResult, ref bool isAutoUseAbility)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var calculatedBestPath = context.BestPath?.vectorPath.Select(v => v.ToNetworkVector3()) ?? [];
            var action = new NetworkAIAction
            {
                UnitId = unit.UniqueId,
                TargetId = bestTargetResult?.UniqueId,
                ActionBlueprintId = bestActionResult?.Blueprint.AssetGuid.ToString(),
                ActionType = bestActionResult?.GetType().Name,
                IsAutoUseAbility = isAutoUseAbility,
                BestPath = [.. calculatedBestPath],
                BestEnableFiveFootStep = context.BestEnableFiveFootStep
            };

            var possibleOverride = Main.Multiplayer.OnAfterAISelectedAction(action);
            if (possibleOverride == null)
            {
                return;
            }

            var requiresContextUpdate = false;
            if (!string.Equals(bestActionResult.Blueprint.AssetGuid.ToString(), possibleOverride.ActionBlueprintId, StringComparison.OrdinalIgnoreCase))
            {
                Main.GetLogger<AiBrainControllerPatches>().LogWarning("Replacing best action result. PreviousActionBlueprintId={PreviousActionBlueprintId}, NewActionBlueprintId={NewActionBlueprintId}", bestActionResult.Blueprint.AssetGuid.ToString(), possibleOverride.ActionBlueprintId);
                bestActionResult = FindAIAction(unit, isAutoUseAbility, possibleOverride);
                requiresContextUpdate = true;
            }

            if (!string.Equals(bestTargetResult?.UniqueId, possibleOverride.TargetId, StringComparison.OrdinalIgnoreCase))
            {
                Main.GetLogger<AiBrainControllerPatches>().LogWarning("Replacing best target result. PreviousTargetUnitId={PreviousTargetUnitId}, NewTargetUnitId={NewTargetUnitId}", bestTargetResult?.UniqueId, possibleOverride.TargetId);
                bestTargetResult = FindActionTarget(possibleOverride.TargetId);
                requiresContextUpdate = true;
            }

            if (requiresContextUpdate)
            {
                context.BestEnableFiveFootStep = possibleOverride.BestEnableFiveFootStep;

                var bestPathOverride = possibleOverride.BestPath.Select(v => v.ToUnityVector3()).ToList();
                context.BestPath = new ForcedPath(bestPathOverride);
            }
        }

        private static UnitEntityData FindActionTarget(string targetUniqueId)
        {
            if (string.IsNullOrEmpty(targetUniqueId))
            {
                return null;
            }

            var target = Game.Instance.State.Units.All.FirstOrDefault(u => string.Equals(u.UniqueId, targetUniqueId, StringComparison.OrdinalIgnoreCase));
            return target;
        }

        private static AiAction FindAIAction(UnitEntityData unitEntityData, bool isAutoUseAbility, NetworkAIAction networkAIAction)
        {
            try
            {
                if (string.IsNullOrEmpty(networkAIAction.ActionBlueprintId))
                {
                    return null;
                }

                if (isAutoUseAbility)
                {
                    var action = unitEntityData.Brain.GetAvailableAutoUseAbility()?.DefaultAiAction;
                    Main.GetLogger<AiBrainControllerPatches>().LogInformation("AutoUse AI action has been selected. ActionBlueprintId={ActionBlueprintId}", action?.Blueprint.AssetGuid.ToString());
                    return action;
                }

                var customAction = unitEntityData.Brain.CustomActions.FirstOrDefault(ca => string.Equals(ca.Blueprint.AssetGuid.ToString(), networkAIAction.ActionBlueprintId));
                if (customAction != null)
                {
                    Main.GetLogger<AiBrainControllerPatches>().LogInformation("Custom AI action has been selected. ActionBlueprintId={ActionBlueprintId}", customAction.Blueprint.AssetGuid.ToString());
                    return customAction;
                }

                var availableAction = unitEntityData.Brain.AvailableActions.FirstOrDefault(ca => string.Equals(ca.Blueprint.AssetGuid.ToString(), networkAIAction.ActionBlueprintId));
                if (availableAction != null)
                {
                    Main.GetLogger<AiBrainControllerPatches>().LogInformation("Available AI action has been selected. ActionBlueprintId={ActionBlueprintId}", availableAction.Blueprint.AssetGuid.ToString());
                    return availableAction;
                }

                Main.GetLogger<AiBrainControllerPatches>().LogError("Unable to find AI action. ActionBlueprintId={ActionBlueprintId}", availableAction.Blueprint.AssetGuid.ToString());
                return null;
            }
            catch (Exception ex)
            {

                Main.GetLogger<AiBrainControllerPatches>().LogInformation(ex, "Error while selecting AI action. UnitId={UnitId}", unitEntityData.UniqueId);
                throw;
            }
        }
    }
}
