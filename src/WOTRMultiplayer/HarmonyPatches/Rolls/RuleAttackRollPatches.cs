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
    public class RuleAttackRollPatches
    {
        [HarmonyPatch(typeof(RuleAttackRoll), nameof(RuleAttackRoll.OnTrigger))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RuleAttackRoll_OnTrigger_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var attr = MethodBase.GetCurrentMethod().GetCustomAttribute<HarmonyPatch>();
            var target = $"{attr.info.declaringType.Name}.{attr.info.methodName}";
            var matcher = new CodeMatcher(instructions);
            var replaceWith = AccessTools.Method(typeof(RuleAttackRollPatches), nameof(RuleAttackRollPatches.AttackRollD20));
            var lookFor = AccessTools.Method(typeof(Dice), nameof(Dice.GenerateD20));
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match == null)
            {
                Main.GetLogger<RuleAttackRollPatches>().LogError("Transpiler has not been applied. Target={target}", target);
                matcher.Instructions();
            }

            match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith)
            };

            match.Insert(newInstructions);
            Main.GetLogger<RuleAttackRollPatches>().LogInformation("Transpiler has been applied. Target={target}", target);

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
    }
}
