using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.RuleSystem.Rules;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches.Rolls
{
    [HarmonyPatch]
    public class RuleSavingThrowPatches
    {
        [HarmonyPatch(typeof(RuleSavingThrow), nameof(RuleSavingThrow.OnTrigger))]
        [HarmonyPostfix]
        public static void RuleSavingThrow_OnTrigger_Postfix(RuleSavingThrow __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Rolls.OnAfterRuleSavingThrowTrigger(__instance);
        }

        [HarmonyPatch(typeof(RuleSavingThrow), nameof(RuleSavingThrow.Calculate))]
        [HarmonyPostfix]
        public static void RuleSavingThrow_Calculate_Postfix(RuleSavingThrow __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Rolls.OnAfterRuleSavingThrowTrigger(__instance);
        }

        [HarmonyPatch(typeof(RuleSavingThrow), nameof(RuleSavingThrow.RollD20))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RuleSavingThrow_RollD20_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            var match = matcher.Advance(1);
            if (match.Instruction.opcode != OpCodes.Ldarg_0)
            {
                Main.GetLogger<RuleSavingThrowPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                matcher.Instructions();
            }

            match = matcher.Advance(1);
            var addMethod = AccessTools.Method(typeof(RuleSavingThrowPatches), nameof(RuleSavingThrowPatches.SavingThrowRollD20));

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Call, addMethod),
                new(OpCodes.Ldarg_0),
            };

            match.Insert(newInstructions);
            Main.GetLogger<RuleSavingThrowPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);

            return matcher.Instructions();
        }

        private static void SavingThrowRollD20(RuleSavingThrow savingThrow)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Rolls.OnBeforeRuleSavingThrowRoll(savingThrow);
        }
    }
}
