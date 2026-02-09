using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UnitLogic.Mechanics;
using Microsoft.Extensions.Logging;

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
            Main.GetLogger<ContextValueHelperPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static int RollDice(MechanicsContext context, RuleRollDice ruleRollDice)
        {
            if (Main.Multiplayer.IsActive && Main.Multiplayer.IsInCombat && ruleRollDice.DiceFormula.Rolls != 0 && ruleRollDice.DiceFormula.Dice != Kingmaker.RuleSystem.DiceType.Zero)
            {
                Main.GetLogger<ContextValueHelperPatches>().LogWarning("Name={Name}, UnitId={UnitId}, TargetUnitId={TargetUnitId}, TargetPoint={TargetPoint}, AbilityBlueprintId={AbilityBlueprintId}, AbilityName={AbilityName}, ItemName={ItemName}, DiceRolls={DiceRolls}, Dice={Dice}",
                    context.NameForAcronym, context.MaybeOwner?.UniqueId, context.MainTarget?.Unit, context.MainTarget?.Point, context.SourceAbility?.AssetGuid.ToString(), context.SourceAbility?.NameForAcronym, context.SourceItem?.NameForAcronym, ruleRollDice.DiceFormula.Rolls, ruleRollDice.DiceFormula.Dice);
            }

            return context.TriggerRule(ruleRollDice).Result;
        }
    }
}
