using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.Controllers.Units;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.GameInteraction.CombatLog;
using WOTRMultiplayer.Extensions;

namespace WOTRMultiplayer.HarmonyPatches.Combat
{
    [HarmonyPatch]
    public class UnitConfusionControllerPatches
    {
        [HarmonyPatch(typeof(UnitConfusionController), nameof(UnitConfusionController.TickConfusion))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> UnitConfusionController_TickConfusion_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(UnitConfusionControllerPatches), nameof(UnitConfusionControllerPatches.RollConfusionEffect));
            var matcher = new CodeMatcher(instructions);
            var lookFor = $"{typeof(RuleRollDice).FullName} {nameof(Rulebook.Trigger)}";
            var match = matcher.SearchForward(x => x.opcode == OpCodes.Call && (x.operand?.ToString().Contains(lookFor) ?? false));
            if (match.IsInvalid)
            {
                Main.GetLogger<UnitConfusionControllerPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith),
            };
            match = match.RemoveInstruction().Insert(newInstructions);

            Main.GetLogger<UnitConfusionControllerPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static RuleRollDice RollConfusionEffect(RuleRollDice ruleRollDice, UnitEntityData unit)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return Rulebook.Trigger(ruleRollDice);
            }

            try
            {
                var seededContext = Main.Multiplayer.GetSeededContext();
                var identifier = $"{nameof(UnitConfusionController)}:{nameof(RollConfusionEffect)}:{unit.UniqueId}_{seededContext.Id}";
                var random = Main.Multiplayer.ValueGenerator.GetRandom(seededContext.Lifetime, identifier);
                var result = ruleRollDice.DiceFormula.Roll(random);
                Main.GetLogger<UnitConfusionControllerPatches>().LogInformation("UnitConfusionController condition has been rolled. UnitId={UnitId}, Roll={Roll}, Identifier={Identifier}", unit.UniqueId, result, identifier);
                Main.PlayerNotification.AddCombatText(WellKnownKeys.GameNotifications.Combat.Conditions.Confusion.Key, CombatTextSeverity.Debug, result, new UnitLogParameter(unit.UniqueId));
                ruleRollDice.m_Triggered = true;
                ruleRollDice.m_Result = result;
                return ruleRollDice;
            }
            catch (Exception ex)
            {
                Main.GetLogger<UnitConfusionControllerPatches>().LogError(ex, "Error while rolling unit confusion. UnitId={UnitId}", unit?.UniqueId);
                throw;
            }
        }
    }
}
