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
    public class RuleInitiativeRollPatches
    {
        [HarmonyPatch(typeof(RuleInitiativeRoll), nameof(RuleInitiativeRoll.OnTrigger))]
        [HarmonyPostfix]
        public static void RuleInitiativeRoll_OnTrigger_Postfix(RuleInitiativeRoll __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Rolls.OnAfterRuleInitiativeRollTrigger(__instance);
        }

        [HarmonyPatch(typeof(RuleInitiativeRoll), nameof(RuleInitiativeRoll.OnTrigger))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RuleInitiativeRoll_OnTrigger_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            var replaceWith = AccessTools.Method(typeof(RuleInitiativeRollPatches), nameof(RuleInitiativeRollPatches.InitiativeRollD20));
            var lookFor = AccessTools.PropertyGetter(typeof(Dice), nameof(Dice.D20));
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<RuleSpellResistancePatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                matcher.Instructions();
            }

            match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith)
            };

            match.Insert(newInstructions);
            Main.GetLogger<RuleSpellResistancePatches>().LogInformation("Transpiler has been applied. Target={Target}", target);

            return matcher.Instructions();
        }

        private static RuleRollD20 InitiativeRollD20(RuleInitiativeRoll ruleInitiativeRoll)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return Dice.D20;
            }

            var shouldRunOriginalLogic = Main.Rolls.OnBeforeRuleInitiativeRoll(ruleInitiativeRoll);
            if (!shouldRunOriginalLogic)
            {
                return ruleInitiativeRoll.D20;
            }

            return Dice.D20;
        }
    }
}
