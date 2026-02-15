using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.AI;
using Kingmaker.Armies.TacticalCombat;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.RuleSystem;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Commands;
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
                var random = Main.Multiplayer.ValueGenerator.GetRandom(IdentifierLifetime.CombatTurn, identifier);
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

        [HarmonyPatch(typeof(AiBrainController), nameof(AiBrainController.SelectAction))]
        [HarmonyPrefix]
        public static bool AiBrainController_SelectAction_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.IsSourceOfAIActions() || TacticalCombatHelper.IsActive;
            return canContinue;
        }

        [HarmonyPatch(typeof(AiBrainController), nameof(AiBrainController.SelectAction))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> AiBrainController_SelectAction_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(UnitCommands), nameof(UnitCommands.InterruptAiCommands));
            var replaceWith = AccessTools.Method(typeof(AiBrainControllerPatches), nameof(AiBrainControllerPatches.OnAIActionSelected));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<ContextValueHelperPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            match = match.Advance(-4);
            var label = match.Instruction.ExtractLabels();
            var newInstructions = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldarg_1).WithLabels(label),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldloc_0),
                new(OpCodes.Ldloc_1),
                new(OpCodes.Call, replaceWith),
            };
            match = match.Insert(newInstructions);
            Main.GetLogger<ContextValueHelperPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static void OnAIActionSelected(DecisionContext decisionContext, UnitEntityData aiUnit, AiAction aiAction, UnitEntityData target)
        {
            if (!Main.Multiplayer.IsActive
                || !Game.Instance.Player.IsInCombat
                || aiAction == null
                || TacticalCombatHelper.IsActive)
            {
                return;
            }

            var action = new NetworkAIAction
            {
                Id = aiAction.Blueprint.AssetGuid.ToString(),
                Name = aiAction.Blueprint.name,
                ActionType = aiAction.GetType().Name,
                UnitId = aiUnit.UniqueId,
                TargetId = target?.UniqueId,
                DecisionContext = new NetworkAIDecisionContext
                {
                    BestEnableFiveFootStep = decisionContext.BestEnableFiveFootStep,
                    VectorPath = decisionContext.BestPath?.vectorPath.Select(v => v.ToNetworkVector3()).ToList() ?? [],
                },
                UseCommand = aiAction.UseCommand
            };

            Main.Multiplayer.OnAIActionSelected(action);
        }
    }
}
