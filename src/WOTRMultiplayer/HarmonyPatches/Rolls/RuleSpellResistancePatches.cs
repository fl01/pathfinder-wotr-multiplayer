using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.RuleSystem.Rules;
using Microsoft.Extensions.Logging;
using static Kingmaker.RuleSystem.RulebookEvent;

namespace WOTRMultiplayer.HarmonyPatches.Rolls
{
    [HarmonyPatch]
    public class RuleSpellResistancePatches
    {
        [HarmonyPatch(typeof(RuleSpellResistanceCheck), nameof(RuleSpellResistanceCheck.OnTrigger))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RuleSpellResistanceCheck_OnTrigger_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            var replaceWith = AccessTools.Method(typeof(RuleSpellResistancePatches), nameof(RuleSpellResistancePatches.SpellResistanceRollD20));
            var lookFor = AccessTools.PropertyGetter(typeof(Dice), nameof(Dice.D20));
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<RuleSpellResistancePatches>().LogError("Transpiler has not been applied. Target={target}", target);
                matcher.Instructions();
            }

            match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith)
            };

            match.Insert(newInstructions);
            Main.GetLogger<RuleSpellResistancePatches>().LogInformation("Transpiler has been applied. Target={target}", target);

            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(RuleSpellResistanceCheck), nameof(RuleSpellResistanceCheck.OnTrigger))]
        [HarmonyPostfix]
        public static void RuleSpellResistanceCheck_OnTrigger_Postfix(RuleSpellResistanceCheck __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Rolls.OnAfterRuleSpellResistanceCheckTrigger(__instance);
        }

        public static RuleRollD20 SpellResistanceRollD20(RuleSpellResistanceCheck ruleSpellResistanceCheck)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return Dice.D20;
            }

            var shouldRunOriginalLogic = Main.Rolls.OnBeforeRuleSpellResistanceCheckRoll(ruleSpellResistanceCheck);
            if (!shouldRunOriginalLogic)
            {
                return ruleSpellResistanceCheck.Roll;
            }

            return Dice.D20;
        }
    }
}
