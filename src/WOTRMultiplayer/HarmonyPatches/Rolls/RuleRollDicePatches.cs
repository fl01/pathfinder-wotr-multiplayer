using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Damage;
using Microsoft.Extensions.Logging;
using static Kingmaker.RuleSystem.RulebookEvent;

namespace WOTRMultiplayer.HarmonyPatches.Rolls
{
    [HarmonyPatch]
    public class RuleRollDicePatches
    {
        [HarmonyPatch(typeof(RuleRollDice), nameof(RuleRollDice.OnTrigger))]
        [HarmonyPostfix]
        public static void RuleRollDice_OnTrigger_Postfix(RuleRollDice __instance, RulebookEventContext context)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnAfterRuleRollDiceTrigger(__instance);
        }

        [HarmonyPatch(typeof(RuleRollDice), nameof(RuleRollDice.OnTrigger))]
        [HarmonyPrefix]
        public static bool RuleRollDice_OnTrigger_Prefix(RuleRollDice __instance, RulebookEventContext context)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var shouldRunOriginal = Main.Multiplayer.OnBeforeRuleRollDiceTrigger(__instance);
            return shouldRunOriginal;
        }

        [HarmonyPatch(typeof(RuleCalculateDamage), nameof(RuleCalculateDamage.OnTrigger))]
        [HarmonyPostfix]
        public static void RuleCalculateDamage_OnTrigger_Postfix(RuleCalculateDamage __instance, RulebookEventContext context)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnAfterRuleCalculateDamageTrigger(__instance);
        }

        [HarmonyPatch(typeof(RuleCalculateDamage), nameof(RuleCalculateDamage.OnTrigger))]
        [HarmonyPrefix]
        public static bool RuleCalculateDamage_OnTrigger_Prefix(RuleCalculateDamage __instance, RulebookEventContext context)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var shouldRunOriginal = Main.Multiplayer.OnBeforeRuleCalculateDamageTrigger(__instance);
            return shouldRunOriginal;
        }


        //[HarmonyPatch(typeof(RuleSavingThrow), nameof(RuleSavingThrow.OnTrigger))]
        //[HarmonyPostfix]
        //public static void RuleSavingThrow_OnTrigger_Postfix(RuleSavingThrow __instance, RulebookEventContext context)
        //{
        //    if (!Main.Multiplayer.IsActive)
        //    {
        //        return;
        //    }

        //    Main.Multiplayer.OnAfterRuleSavingThrowTrigger(__instance);
        //}

        //[HarmonyPatch(typeof(RuleSavingThrow), nameof(RuleSavingThrow.OnTrigger))]
        //[HarmonyPrefix]
        //public static bool RuleSavingThrow_OnTrigger_Prefix(RuleSavingThrow __instance, RulebookEventContext context)
        //{
        //    if (!Main.Multiplayer.IsActive)
        //    {
        //        return true;
        //    }

        //    var shouldRunOriginal = Main.Multiplayer.OnBeforeRuleSavingThrowTrigger(__instance);
        //    return shouldRunOriginal;
        //}

        [HarmonyPatch(typeof(RuleHealDamage), nameof(RuleHealDamage.Roll))]
        [HarmonyPostfix]
        public static void RuleHealDamage_Roll_Postfix(RuleHealDamage __instance, int unitsCount, ref int __result)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            __result = Main.Multiplayer.OnAfterRollRuleHealDamage(__instance, unitsCount, __result);
        }

        [HarmonyPatch(typeof(RuleSavingThrow), nameof(RuleSavingThrow.OnTrigger))]
        [HarmonyPostfix]
        public static void RuleSavingThrow_OnTrigger_Postfix(RuleSavingThrow __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnAfterRuleSavingThrowTrigger(__instance);
        }

        [HarmonyPatch(typeof(RuleSavingThrow), nameof(RuleSavingThrow.RollD20))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RuleSavingThrow_RollD20_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var attr = MethodBase.GetCurrentMethod().GetCustomAttribute<HarmonyPatch>();
            var target = $"{attr.info.declaringType.Name}.{attr.info.methodName}";
            var matcher = new CodeMatcher(instructions);
            var match = matcher.Advance(1);
            if (match.Instruction.opcode != OpCodes.Ldarg_0)
            {
                Main.GetLogger<HarmonyTranspiler>().LogError("Transpiler has not been applied. Target={target}", target);
                matcher.Instructions();
            }

            match = matcher.Advance(1);
            var addMethod = AccessTools.Method(typeof(RuleRollDicePatches), nameof(RuleRollDicePatches.SavingThrowRollD20));

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Call, addMethod),
                new(OpCodes.Ldarg_0),
            };

            match.Insert(newInstructions);
            Main.GetLogger<HarmonyTranspiler>().LogInformation("Transpiler has been applied. Target={target}", target);

            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(RuleAttackRoll), nameof(RuleAttackRoll.OnTrigger))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RuleAttackRoll_OnTrigger_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var attr = MethodBase.GetCurrentMethod().GetCustomAttribute<HarmonyPatch>();
            var target = $"{attr.info.declaringType.Name}.{attr.info.methodName}";
            var matcher = new CodeMatcher(instructions);
            var replaceWith = AccessTools.Method(typeof(RuleRollDicePatches), nameof(RuleRollDicePatches.AttackRollD20));
            var lookFor = AccessTools.Method(typeof(Dice), nameof(Dice.GenerateD20));
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match == null)
            {
                Main.GetLogger<HarmonyTranspiler>().LogError("Transpiler has not been applied. Target={target}", target);
                matcher.Instructions();
            }

            match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith)
            };

            match.Insert(newInstructions);
            Main.GetLogger<HarmonyTranspiler>().LogInformation("Transpiler has been applied. Target={target}", target);

            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(RuleAttackRoll), nameof(RuleAttackRoll.OnTrigger))]
        [HarmonyPostfix]
        public static void RuleAttackRoll_OnTrigger_Postfix(RuleAttackRoll __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnAfterRuleAttackRollTrigger(__instance);
        }

        [HarmonyPatch(typeof(RuleSpellResistanceCheck), nameof(RuleSpellResistanceCheck.OnTrigger))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RuleSpellResistanceCheck_OnTrigger_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var attr = MethodBase.GetCurrentMethod().GetCustomAttribute<HarmonyPatch>();
            var target = $"{attr.info.declaringType.Name}.{attr.info.methodName}";
            var matcher = new CodeMatcher(instructions);
            var replaceWith = AccessTools.Method(typeof(RuleRollDicePatches), nameof(RuleRollDicePatches.SpellResistanceRollD20));
            var lookFor = AccessTools.PropertyGetter(typeof(Dice), nameof(Dice.D20));
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match == null)
            {
                Main.GetLogger<HarmonyTranspiler>().LogError("Transpiler has not been applied. Target={target}", target);
                matcher.Instructions();
            }

            match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith)
            };

            match.Insert(newInstructions);
            Main.GetLogger<HarmonyTranspiler>().LogInformation("Transpiler has been applied. Target={target}", target);

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

            Main.Multiplayer.OnAfterRuleSpellResistanceCheckTrigger(__instance);
        }

        public static RuleRollD20 AttackRollD20(bool isFake, RuleAttackRoll ruleAttackRoll)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return Dice.GenerateD20(isFake);
            }

            var shouldRunOriginalLogic = Main.Multiplayer.OnBeforeRuleAttackRoll(ruleAttackRoll);
            if (!shouldRunOriginalLogic)
            {
                return ruleAttackRoll.D20;
            }

            var roll = Dice.GenerateD20(isFake);
            return roll;
        }

        public static void SavingThrowRollD20(RuleSavingThrow savingThrow)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnBeforeRuleSavingThrowRoll(savingThrow);
        }

        public static RuleRollD20 SpellResistanceRollD20(RuleSpellResistanceCheck ruleSpellResistanceCheck)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return Dice.D20;
            }

            var shouldRunOriginalLogic = Main.Multiplayer.OnBeforeRuleSpellResistanceCheckRoll(ruleSpellResistanceCheck);
            if (!shouldRunOriginalLogic)
            {
                return ruleSpellResistanceCheck.Roll;
            }

            return Dice.D20;
        }
    }

    // RuleStatCheck
    // RuleSkillCheck
    //+ RuleSpellResistance
    //+ RuleAttackRoll
    //+ RuleSavingThrow

    //+ RuleCalculateDamage
    //+ RuleHealDamage
}
