using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.RuleSystem.Rules;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches.Rolls
{
    /// <summary>
    /// RuleStatCheck + RuleSkillCheck
    /// </summary>
    [HarmonyPatch]
    public class RuleSkillCheckPatches
    {

        [HarmonyPatch(typeof(RuleSkillCheck), nameof(RuleSkillCheck.OnTrigger))]
        [HarmonyPostfix]
        public static void RuleSkillCheck_OnTrigger_Postfix(RuleSkillCheck __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnAfterRuleSkillCheckTrigger(__instance);
        }

        [HarmonyPatch(typeof(RuleSkillCheck), nameof(RuleSkillCheck.OnTrigger))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RuleSkillCheck_OnTrigger_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var currentMethod = MethodBase.GetCurrentMethod();
            var attr = currentMethod.GetCustomAttribute<HarmonyPatch>();
            var target = $"{attr.info.declaringType.Name}.{attr.info.methodName} ({currentMethod.Name})";
            var matcher = new CodeMatcher(instructions);
            var replaceWith = AccessTools.Method(typeof(RuleSkillCheckPatches), nameof(RuleSkillCheckPatches.SkillCheckRollD20));
            if (!ReplaceRollD20(target, matcher, replaceWith) || !ReplaceFromIntUnityRange(target, matcher, replaceWith))
            {
                Main.GetLogger<RuleSkillCheckPatches>().LogError("Transpiler has not been applied. Target={target}", target);
                return instructions;
            }

            Main.GetLogger<RuleSkillCheckPatches>().LogInformation("Transpiler has been applied. Target={target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(RuleSkillCheck), nameof(RuleSkillCheck.Calculate))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RuleSkillCheck_Calculate_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var currentMethod = MethodBase.GetCurrentMethod();
            var attr = currentMethod.GetCustomAttribute<HarmonyPatch>();
            var target = $"{attr.info.declaringType.Name}.{attr.info.methodName} ({currentMethod.Name})";
            var matcher = new CodeMatcher(instructions);
            var replaceWith = AccessTools.Method(typeof(RuleSkillCheckPatches), nameof(RuleSkillCheckPatches.SkillCheckRollD20));
            if (!ReplaceRollD20(target, matcher, replaceWith))
            {
                Main.GetLogger<RuleSkillCheckPatches>().LogError("Transpiler has not been applied. Target={target}", target);
                return instructions;
            }

            Main.GetLogger<RuleSkillCheckPatches>().LogInformation("Transpiler has been applied. Target={target}", target);
            return matcher.Instructions();
        }

        public static RuleRollD20 SkillCheckRollD20(RuleSkillCheck ruleSkillCheck, int minRoll, int maxRoll)
        {
            RuleRollD20 originalFunc() => minRoll == 0 ? ruleSkillCheck.RollD20()
                : RuleRollD20.FromInt(ruleSkillCheck.Initiator, UnityEngine.Random.Range(minRoll, maxRoll));

            if (!Main.Multiplayer.IsActive)
            {
                return originalFunc();
            }

            var shouldRunOriginalLogic = Main.Multiplayer.OnBeforeRuleSkillCheckRoll(ruleSkillCheck);
            if (!shouldRunOriginalLogic)
            {
                return ruleSkillCheck.D20;
            }

            return originalFunc();
        }

        private static bool ReplaceRollD20(string target, CodeMatcher matcher, MethodInfo replaceWith)
        {
            var lookFor = AccessTools.Method(typeof(RuleSkillCheck), nameof(RuleSkillCheck.RollD20));
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match == null)
            {
                Main.GetLogger<RuleSkillCheckPatches>().LogError("{transpilerPart} - unable to find target method. MethodName={methodName}", target, lookFor.Name);
                return false;
            }

            match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldc_I4, 0),
                new(OpCodes.Ldc_I4, 0),
                new(OpCodes.Call, replaceWith)
            };
            match.Insert(newInstructions);
            return true;
        }

        private static bool ReplaceFromIntUnityRange(string target, CodeMatcher matcher, MethodInfo replaceWith)
        {
            var lookFor = AccessTools.Method(typeof(RuleRollD20), nameof(RuleRollD20.FromInt));
            var instructionsEnd = matcher.SearchForward(x => x.Calls(lookFor));
            if (instructionsEnd == null)
            {
                Main.GetLogger<RuleSkillCheckPatches>().LogError("{transpilerPart} - unable to find target method. MethodName={methodName}", target, lookFor.Name);
                return false;
            }

            var instructionsStart = instructionsEnd.SearchBackwards(x => x.opcode == OpCodes.Ldfld);
            if (instructionsStart == null)
            {
                Main.GetLogger<RuleSkillCheckPatches>().LogError("{transpilerPart} - unable to find first instruction to remove", target);
                return false;
            }

            var instructionsToRemove = instructionsEnd.Pos - instructionsStart.Pos;
            instructionsStart.RemoveInstructions(instructionsToRemove);
            var newInstructions = new List<CodeInstruction>()
            {
                // reload min,max values since they have been deleted in the RemoveInstructions call
                new(OpCodes.Ldloc_1),
                new(OpCodes.Ldloc_2),
                new(OpCodes.Call, replaceWith)
            };
            instructionsEnd.Insert(newInstructions);
            return true;
        }
    }
}
