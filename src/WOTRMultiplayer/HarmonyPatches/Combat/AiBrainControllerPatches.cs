using System;
using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.AI;
using Kingmaker.EntitySystem.Entities;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.MP.Entities.Combat;

namespace WOTRMultiplayer.HarmonyPatches.Combat
{
    [HarmonyPatch]
    public class AiBrainControllerPatches
    {
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

        [HarmonyPatch(typeof(AiBrainController), nameof(AiBrainController.FindBestAction))]
        [HarmonyPostfix]
        public static void AiBrainController_FindBestAction_Postfix(AiBrainController __instance, UnitEntityData unit, DecisionContext context, ref AiAction bestActionResult, ref UnitEntityData bestTargetResult, bool isAutoUseAbility)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var action = new NetworkAIAction
            {
                CurrentScore = context.CurrentScore,
                UnitId = unit.UniqueId,
                TargetId = bestTargetResult?.UniqueId,
                ActionBlueprintId = bestActionResult?.Blueprint.AssetGuid.ToString(),
                ActionType = bestActionResult?.GetType().Name,
                IsAutoUseAbility = isAutoUseAbility
            };

            var possibleOverride = Main.Multiplayer.OnAfterAISelectedAction(action);
            if (possibleOverride == null)
            {
                return;
            }

            if (!string.Equals(bestActionResult.Blueprint.AssetGuid.ToString(), possibleOverride.ActionBlueprintId, StringComparison.OrdinalIgnoreCase))
            {
                Main.GetLogger<AiBrainControllerPatches>().LogWarning("Replacing best action result. NewActionBlueprintId={NewActionBlueprintId}", possibleOverride.ActionBlueprintId);
                bestActionResult = FindAIAction(unit, isAutoUseAbility, possibleOverride);
            }

            if (!string.Equals(bestTargetResult?.UniqueId, possibleOverride.TargetId, StringComparison.OrdinalIgnoreCase))
            {
                Main.GetLogger<AiBrainControllerPatches>().LogWarning("Replacing best target result. NewTargetUnitId={NewTargetUnitId}", possibleOverride.TargetId);
                bestTargetResult = FindActionTarget(possibleOverride.TargetId);
            }

            context.CurrentScore = possibleOverride.CurrentScore;
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
                    Main.GetLogger<AiBrainControllerPatches>().LogInformation("Custom AI action has been selected. ActionBlueprintId={ActionBlueprintId}", customAction?.Blueprint.AssetGuid.ToString());
                    return customAction;
                }

                var availableAction = unitEntityData.Brain.AvailableActions.FirstOrDefault(ca => string.Equals(ca.Blueprint.AssetGuid.ToString(), networkAIAction.ActionBlueprintId));
                if (availableAction != null)
                {
                    Main.GetLogger<AiBrainControllerPatches>().LogInformation("Available AI action has been selected. ActionBlueprintId={ActionBlueprintId}", availableAction?.Blueprint.AssetGuid.ToString());
                    return availableAction;
                }

                Main.GetLogger<AiBrainControllerPatches>().LogError("Unable to find AI action. ActionBlueprintId={ActionBlueprintId}", availableAction?.Blueprint.AssetGuid.ToString());
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
