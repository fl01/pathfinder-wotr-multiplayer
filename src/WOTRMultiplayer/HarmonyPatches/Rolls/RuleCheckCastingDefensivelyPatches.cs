using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Abilities;
using Microsoft.Extensions.Logging;
using static Kingmaker.RuleSystem.RulebookEvent;

namespace WOTRMultiplayer.HarmonyPatches.Rolls
{
    [HarmonyPatch]
    public class RuleCheckCastingDefensivelyPatches
    {
        [HarmonyPatch(typeof(RuleCheckCastingDefensively), nameof(RuleCheckCastingDefensively.OnTrigger))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RuleCheckCastingDefensively_OnTrigger_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            var replaceWith = AccessTools.Method(typeof(RuleCheckCastingDefensivelyPatches), nameof(RuleCheckCastingDefensivelyPatches.CheckCastingDefensivelyRollD20));
            var lookFor = AccessTools.PropertyGetter(typeof(Dice), nameof(Dice.D20));
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<RuleCheckCastingDefensivelyPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                matcher.Instructions();
            }

            match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith)
            };

            match.Insert(newInstructions);
            Main.GetLogger<RuleCheckCastingDefensivelyPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);

            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(RuleCheckCastingDefensively), nameof(RuleCheckCastingDefensively.OnTrigger))]
        [HarmonyPostfix]
        public static void RuleCheckCastingDefensively_OnTrigger_Postfix(RuleCheckCastingDefensively __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Rolls.OnAfterRuleCheckCastingDefensivelyTrigger(__instance);
        }

        private static RuleRollD20 CheckCastingDefensivelyRollD20(RuleCheckCastingDefensively ruleCheckCastingDefensively)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return Dice.D20;
            }

            var shouldRunOriginalLogic = Main.Rolls.OnBeforeRuleCheckCastingDefensivelyRoll(ruleCheckCastingDefensively);
            if (!shouldRunOriginalLogic)
            {
                return ruleCheckCastingDefensively.D20;
            }

            return Dice.D20;
        }
    }
}
