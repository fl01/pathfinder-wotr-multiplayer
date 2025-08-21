using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Microsoft.Extensions.Logging;
using static Kingmaker.RuleSystem.RulebookEvent;

namespace WOTRMultiplayer.HarmonyPatches.Rolls
{
    [HarmonyPatch]
    public class RuleAttackRollPatches
    {

        [HarmonyPatch(typeof(RuleAttackRoll), nameof(RuleAttackRoll.OnTrigger))]
        [HarmonyPostfix]
        public static void RuleAttackRoll_OnTrigger_Postfix(RuleAttackRoll __instance)
        {
            if (!Main.Multiplayer.IsActive || PatchesUtils.IsHelperUnit(__instance.Initiator.UniqueId) || PatchesUtils.IsHelperUnit(__instance.Target.UniqueId))
            {
                return;
            }

            Main.Rolls.OnAfterRuleAttackRollTrigger(__instance);
        }

        /// <summary>
        /// D20 + CriticalD20 + Fortificationd100
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        [HarmonyPatch(typeof(RuleAttackRoll), nameof(RuleAttackRoll.OnTrigger))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RuleAttackRoll_OnTrigger_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            if (!ReplaceD20Rolls(target, matcher) || !ReplaceForitifactionRoll(target, matcher))
            {
                return instructions;
            }

            Main.GetLogger<RuleAttackRollPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(RuleAttackRoll), nameof(RuleAttackRoll.TryOvercomeTargetConcealmentAndMissChance))]
        [HarmonyPostfix]
        public static void RuleAttackRoll_TryOvercomeTargetConcealmentAndMissChance_Postfix(RuleAttackRoll __instance)
        {
            if (!Main.Multiplayer.IsActive || PatchesUtils.IsHelperUnit(__instance.Initiator.UniqueId) || PatchesUtils.IsHelperUnit(__instance.Target.UniqueId))
            {
                return;
            }

            Main.Rolls.OnAfterRuleAttackOvercomeConcealmentRoll(__instance);
        }

        [HarmonyPatch(typeof(RuleAttackRoll), nameof(RuleAttackRoll.TryOvercomeTargetConcealmentAndMissChance))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RuleAttackRoll_TryOvercomeTargetConcealmentAndMissChance_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            var replaceWith = AccessTools.Method(typeof(RuleAttackRollPatches), nameof(RuleAttackRollPatches.OvercomeConcealmentRollD100));
            var lookFor = AccessTools.PropertyGetter(typeof(RuleAttackRoll), nameof(RuleAttackRoll.MissChanceRoll));
            CodeMatcher match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<RuleAttackRollPatches>().LogError("Transpiler has no been applied. Target={Target}", target);
                return instructions;
            }

            match.RemoveInstructions(2);
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Call, replaceWith)
            };
            match.Insert(newInstructions);

            return matcher.Instructions();
        }

        private static bool ReplaceForitifactionRoll(string target, CodeMatcher matcher)
        {
            var replaceWith = AccessTools.Method(typeof(RuleAttackRollPatches), nameof(RuleAttackRollPatches.ForitifactionRollD100));
            var lookFor = AccessTools.PropertyGetter(typeof(Dice), nameof(Dice.D100));
            CodeMatcher match = matcher.Start().SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<RuleAttackRollPatches>().LogError("Transpiler has no been applied. Target={Target}, Position={Position}", target, match.Pos);
                return false;
            }

            match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith)
            };
            match.Insert(newInstructions);

            return true;
        }

        private static bool ReplaceD20Rolls(string target, CodeMatcher matcher)
        {
            var replaceWith = AccessTools.Method(typeof(RuleAttackRollPatches), nameof(RuleAttackRollPatches.AttackRollD20));
            var lookFor = AccessTools.Method(typeof(Dice), nameof(Dice.GenerateD20));
            CodeMatcher match;
            var replacementCounter = 0;
            while ((match = matcher.SearchForward(x => x.Calls(lookFor))).IsValid)
            {
                match.RemoveInstruction();
                var newInstructions = new List<CodeInstruction>()
                {
                    new(OpCodes.Ldarg_0),
                    new(OpCodes.Ldc_I4, replacementCounter), // IsCriticall=true for critical roll replacement
                    new(OpCodes.Call, replaceWith)
                };
                match.Insert(newInstructions);
                replacementCounter++;
            }

            const int ExpectedReplacementCounter = 2;
            if (replacementCounter != ExpectedReplacementCounter)
            {
                Main.GetLogger<RuleAttackRollPatches>().LogError("Instructions have not been replaced expected number of times. Target={Target}, Expected={Expected}, Current={Current}", target, ExpectedReplacementCounter, replacementCounter);
                return false;
            }

            return true;
        }

        public static RuleRollD20 AttackRollD20(bool isFake, RuleAttackRoll ruleAttackRoll, bool isCriticalRoll)
        {
            if (!Main.Multiplayer.IsActive || PatchesUtils.IsHelperUnit(ruleAttackRoll.Initiator.UniqueId) || PatchesUtils.IsHelperUnit(ruleAttackRoll.Target.UniqueId))
            {
                return Dice.GenerateD20(isFake);
            }

            var shouldRunOriginalLogic = Main.Rolls.OnBeforeRuleAttackRoll(ruleAttackRoll, isCriticalRoll);
            if (!shouldRunOriginalLogic)
            {
                return ruleAttackRoll.D20;
            }

            var roll = Dice.GenerateD20(isFake);
            return roll;
        }

        public static RuleRollD100 ForitifactionRollD100(RuleAttackRoll ruleAttackRoll)
        {
            if (!Main.Multiplayer.IsActive || PatchesUtils.IsHelperUnit(ruleAttackRoll.Initiator.UniqueId) || PatchesUtils.IsHelperUnit(ruleAttackRoll.Target.UniqueId))
            {
                return RulebookEvent.Dice.D100;
            }

            var shouldRunOriginalLogic = Main.Rolls.OnBeforeRuleAttackFortificationRoll(ruleAttackRoll);
            if (!shouldRunOriginalLogic)
            {
                return ruleAttackRoll.FortificationRoll;
            }

            var roll = RulebookEvent.Dice.D100;
            return roll;
        }

        public static RuleRollD100 OvercomeConcealmentRollD100(RuleAttackRoll ruleAttackRoll)
        {
            if (!Main.Multiplayer.IsActive || PatchesUtils.IsHelperUnit(ruleAttackRoll.Initiator.UniqueId) || PatchesUtils.IsHelperUnit(ruleAttackRoll.Target.UniqueId))
            {
                return RulebookEvent.Dice.D100;
            }

            var shouldRunOriginalLogic = Main.Rolls.OnBeforeRuleAttackOvercomeConcealmentRoll(ruleAttackRoll);
            if (!shouldRunOriginalLogic)
            {
                return ruleAttackRoll.MissChanceRoll;
            }

            var roll = RulebookEvent.Dice.D100;
            return roll;
        }
    }
}
