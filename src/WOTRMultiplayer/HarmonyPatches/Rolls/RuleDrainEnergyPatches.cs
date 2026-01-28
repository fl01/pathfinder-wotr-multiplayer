using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches.Rolls
{
    [HarmonyPatch]
    public class RuleDrainEnergyPatches
    {
        [HarmonyPatch(typeof(RuleDrainEnergy), nameof(RuleDrainEnergy.OnTrigger))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RuleDrainEnergy_OnTrigger_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(RuleDrainEnergyPatches), nameof(RuleDrainEnergyPatches.TriggerRoll));
            var matcher = new CodeMatcher(instructions);
            var lookFor = $"{typeof(RuleRollDice).FullName} {nameof(Rulebook.Trigger)}";
            var match = matcher.SearchForward(x => x.opcode == OpCodes.Call && (x.operand?.ToString().Contains(lookFor) ?? false));
            if (match.IsInvalid)
            {
                Main.GetLogger<RuleDrainEnergyPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith),
            };

            match = match.RemoveInstruction().Insert(newInstructions);
            PatchesUtils.Dump(match);
            Main.GetLogger<RuleDrainEnergyPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        /// <summary>
        /// base game logic to call Rulebook.Trigger(d20), but with extra roll hooks
        /// </summary>
        /// <param name="ruleRollDice"></param>
        /// <param name="ruleDrainEnergy"></param>
        /// <returns></returns>
        private static RuleRollDice TriggerRoll(RuleRollDice ruleRollDice, RuleDrainEnergy ruleDrainEnergy)
        {
            if (Main.Multiplayer.IsActive)
            {
                var shouldRunOriginalLogic = Main.Rolls.OnBeforeRuleDrainEnergyRoll(ruleDrainEnergy, ruleRollDice);
                if (!shouldRunOriginalLogic)
                {
                    return ruleRollDice;
                }
            }

            var roll = Rulebook.Trigger(ruleRollDice);

            if (Main.Multiplayer.IsActive)
            {
                Main.Rolls.OnAfterRuleDrainEnergyRoll(ruleDrainEnergy, roll);
            }

            return roll;
        }
    }
}
