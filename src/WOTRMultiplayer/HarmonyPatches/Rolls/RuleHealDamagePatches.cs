using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.Armies.TacticalCombat;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules.Damage;
using Microsoft.Extensions.Logging;
using static Kingmaker.RuleSystem.RulebookEvent;

namespace WOTRMultiplayer.HarmonyPatches.Rolls
{
    [HarmonyPatch]
    public class RuleHealDamagePatches
    {
        [HarmonyPatch(typeof(RuleHealDamage), nameof(RuleHealDamage.Roll))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RuleHealDamage_Roll_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            if (!ReplaceNonTacticalCombat(matcher, target) || !ReplaceTacticalCombat(matcher, target))
            {
                Main.GetLogger<RuleHealDamagePatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                matcher.Instructions();
            }


            return matcher.Instructions();
        }

        private static bool ReplaceNonTacticalCombat(CodeMatcher matcher, string target)
        {
            var lookFor = AccessTools.Method(typeof(Dice), nameof(Dice.D), [typeof(DiceFormula)]);
            var replaceWith = AccessTools.Method(typeof(RuleHealDamagePatches), nameof(RuleHealDamagePatches.HealDamageRoll));
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<RuleHealDamagePatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return false;
            }
            match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_1),
                new(OpCodes.Ldc_I4_0),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith)
            };
            match.Insert(newInstructions);
            Main.GetLogger<RuleHealDamagePatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return true;
        }

        private static bool ReplaceTacticalCombat(CodeMatcher matcher, string target)
        {
            var lookFor = AccessTools.Method(typeof(TacticalCombatHelper), nameof(TacticalCombatHelper.GetDiceResult));
            var replaceWith = AccessTools.Method(typeof(RuleHealDamagePatches), nameof(RuleHealDamagePatches.HealDamageRoll));
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<RuleHealDamagePatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return false;
            }

            match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_1),
                new(OpCodes.Ldc_I4_1),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith)
            };
            match.Insert(newInstructions);
            Main.GetLogger<RuleHealDamagePatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return true;
        }


        [HarmonyPatch(typeof(RuleHealDamage), nameof(RuleHealDamage.Roll))]
        [HarmonyPostfix]
        public static void RuleHealDamage_Roll_Postfix(RuleHealDamage __instance, int unitsCount, ref int __result)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Rolls.OnAfterRollRuleHealDamage(__instance, unitsCount, __result, TacticalCombatHelper.IsActive);
        }

        public static int HealDamageRoll(DiceFormula diceFormula, int unitsCount, bool isTacticalCombat, RuleHealDamage ruleHealDamage)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return isTacticalCombat ? TacticalCombatHelper.GetDiceResult(diceFormula) : Dice.D(diceFormula);
            }

            var shouldRunOriginalLogic = Main.Rolls.OnBeforeRollRuleHealDamage(ruleHealDamage, unitsCount, isTacticalCombat);
            if (!shouldRunOriginalLogic)
            {
                return ruleHealDamage.RollResult;
            }

            return isTacticalCombat ? TacticalCombatHelper.GetDiceResult(diceFormula) : Dice.D(diceFormula);
        }
    }
}
