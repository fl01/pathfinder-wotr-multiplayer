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
    public class RuleDispelMagicPatches
    {
        /// <summary>
        /// RuleDispelMagic.CheckType.None => this.CheckRoll = Rulebook.Trigger<RuleRollD20>(ruleRollD); is kept intact since roll is overriden by 20 anyway
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        [HarmonyPatch(typeof(RuleDispelMagic), nameof(RuleDispelMagic.OnTrigger))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RuleDispelMagic_OnTrigger_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            var replaceWith = AccessTools.Method(typeof(RuleDispelMagicPatches), nameof(DispelMagicRollD20));
            var lookFor = AccessTools.PropertyGetter(typeof(Dice), nameof(Dice.D20));
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<RuleDispelMagicPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                matcher.Instructions();
            }

            match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith)
            };

            match.Insert(newInstructions);
            Main.GetLogger<RuleDispelMagicPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);

            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(RuleDispelMagic), nameof(RuleDispelMagic.OnTrigger))]
        [HarmonyPostfix]
        public static void RuleDispelMagic_OnTrigger_Postfix(RuleDispelMagic __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Rolls.OnAfterRuleDispelMagicTrigger(__instance);
        }

        public static RuleRollD20 DispelMagicRollD20(RuleDispelMagic ruleDispelMagic)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return Dice.D20;
            }

            var shouldRunOriginalLogic = Main.Rolls.OnBeforeRuleDispelMagicRoll(ruleDispelMagic);
            if (!shouldRunOriginalLogic)
            {
                return ruleDispelMagic.CheckRoll;
            }

            return Dice.D20;
        }
    }
}
