using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UnitLogic.Mechanics;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Extensions;

namespace WOTRMultiplayer.HarmonyPatches.Combat
{
    [HarmonyPatch]
    public class ContextValueHelperPatches
    {
        [HarmonyPatch(typeof(ContextValueHelper), nameof(ContextValueHelper.CalculateDiceValue))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> ContextValueHelper_CalculateDiceValue_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.PropertyGetter(typeof(RuleRollDice), nameof(RuleRollDice.Result));
            var replaceWith = AccessTools.Method(typeof(ContextValueHelperPatches), nameof(ContextValueHelperPatches.RollDice));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<ContextValueHelperPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Call, replaceWith),
            };
            match = match.Advance(-1).RemoveInstructions(2).Insert(newInstructions);
            Main.GetLogger<ContextValueHelperPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static int RollDice(MechanicsContext context, RuleRollDice ruleRollDice)
        {
            if (!Main.Multiplayer.IsActive
                || ruleRollDice.DiceFormula.Rolls == 0
                || ruleRollDice.DiceFormula.Dice == Kingmaker.RuleSystem.DiceType.Zero
                || PatchesUtils.IsHelperUnit(ruleRollDice.Initiator.UniqueId))
            {
                return context.TriggerRule(ruleRollDice).Result;
            }

            var unitId = context.MaybeOwner?.UniqueId;
            var targetUnitId = context?.MainTarget?.Unit?.UniqueId;
            try
            {
                var seededContext = Main.Multiplayer.GetSeededContext();
                var abilityName = context.SourceAbility?.NameForAcronym;
                var itemName = context.SourceItem?.NameForAcronym;
                var identifier = $"{nameof(ContextValueHelper)}:{nameof(RollDice)}:{context.NameForAcronym}:{unitId}:{targetUnitId}:{context.SourceAbility?.AssetGuid.ToString()}:{abilityName}:{itemName}:{ruleRollDice.DiceFormula.Rolls}:{ruleRollDice.DiceFormula.Dice}_{seededContext.Id}";
                var random = Main.Multiplayer.ValueGenerator.GetRandom(seededContext.Lifetime, identifier);
                var result = ruleRollDice.DiceFormula.Roll(random);
                Main.GetLogger<ContextValueHelperPatches>().LogInformation("Ability/Item random value has been rolled. Name={Name}, Result={Result}, UnitId={UnitId}, TargetUnitId={TargetUnitId}, AbilityName={AbilityName}, ItemName={ItemName}, DiceRolls={DiceRolls}, Dice={Dice}, IdentifierLifetime={IdentifierLifetime}, Identifier={Identifier}",
                    context.NameForAcronym, result, unitId, targetUnitId, abilityName, itemName, ruleRollDice.DiceFormula.Rolls, ruleRollDice.DiceFormula.Dice, seededContext.Lifetime, identifier);
                return result;
            }
            catch (Exception ex)
            {
                Main.GetLogger<ContextValueHelperPatches>().LogError(ex, "Error while rolling context value. Name={Name}, UnitId={UnitId}, TargetUnitId={TargetUnitId}", context.NameForAcronym, unitId, targetUnitId);
                throw;
            }
        }
    }
}
