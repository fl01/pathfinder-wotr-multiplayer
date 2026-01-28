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
    public class RuleEnterStealthPatches
    {
        [HarmonyPatch(typeof(RuleEnterStealth), nameof(RuleEnterStealth.OnTrigger))]
        [HarmonyPostfix]
        public static void RuleEnterStealth_OnTrigger_Postfix(RuleEnterStealth __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Rolls.OnAfterRuleEnterStealthTrigger(__instance);
        }

        [HarmonyPatch(typeof(RuleEnterStealth), nameof(RuleEnterStealth.OnTrigger))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RuleEnterStealth_OnTrigger_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(RuleEnterStealthPatches), nameof(RuleEnterStealthPatches.RollD20));
            var matcher = new CodeMatcher(instructions);
            var lookFor = $"{typeof(RuleRollD20).FullName} {nameof(Rulebook.Trigger)}";
            var match = matcher.SearchForward(x => x.opcode == OpCodes.Call && (x.operand?.ToString().Contains(lookFor) ?? false));
            if (match.IsInvalid)
            {
                Main.GetLogger<RuleEnterStealth>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Call, replaceWith),
                new(OpCodes.Ldarg_0),
            };

            match = match.RemoveInstruction().Insert(newInstructions);
            Main.GetLogger<RuleEnterStealth>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static RuleRollD20 RollD20(RuleRollD20 d20, RuleEnterStealth ruleEnterStealth)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return Rulebook.Trigger(d20);
            }

            var shouldContinue = Main.Rolls.OnBeforeRuleEnterStealthRoll(ruleEnterStealth);
            if (!shouldContinue)
            {
                return ruleEnterStealth.D20;
            }

            return Rulebook.Trigger(d20);
        }
    }
}
