using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Abilities;
using Microsoft.Extensions.Logging;
using static Kingmaker.RuleSystem.RulebookEvent;

namespace WOTRMultiplayer.HarmonyPatches.Rolls
{
    [HarmonyPatch]
    public class RuleCheckConcentrationPatches
    {
        [HarmonyPatch(typeof(RuleCheckConcentration), nameof(RuleCheckConcentration.OnTrigger))]
        [HarmonyPostfix]
        public static void RuleCheckConcentration_OnTrigger_Postfix(RuleCheckConcentration __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Rolls.OnAfterRuleCheckConcentrationTrigger(__instance);
        }

        [HarmonyPatch(typeof(RuleCheckConcentration), nameof(RuleCheckConcentration.OnTrigger))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RuleCheckConcentration_OnTrigger_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            var replaceWith = AccessTools.Method(typeof(RuleCheckConcentrationPatches), nameof(RuleCheckConcentrationPatches.CheckConcentrationRollD20));
            var lookFor = AccessTools.PropertyGetter(typeof(Dice), nameof(Dice.D20));
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<RuleCheckConcentrationPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return matcher.Instructions();
            }

            match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith)
            };

            match.Insert(newInstructions);
            Main.GetLogger<RuleCheckConcentrationPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);

            return matcher.Instructions();
        }

        public static RuleRollD20 CheckConcentrationRollD20(RuleCheckConcentration ruleCheckConcentration)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return Dice.D20;
            }

            var shouldRunOriginalLogic = Main.Rolls.OnBeforeRuleCheckConcentrationRoll(ruleCheckConcentration);
            if (!shouldRunOriginalLogic)
            {
                return ruleCheckConcentration.ResultRollRaw;
            }

            return Dice.D20;
        }
    }
}
