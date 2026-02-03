using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.RuleSystem.Rules.Damage;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches.Rolls
{
    [HarmonyPatch]
    public class RuleCalculateDamagePatches
    {
        [HarmonyPatch(typeof(RuleCalculateDamage), nameof(RuleCalculateDamage.OnTrigger))]
        [HarmonyPostfix]
        public static void RuleCalculateDamage_OnTrigger_Postfix(RuleCalculateDamage __instance)
        {
            if (!Main.Multiplayer.IsActive || PatchesUtils.IsHelperUnit(__instance.Target.UniqueId))
            {
                return;
            }

            Main.Rolls.OnAfterRuleCalculateDamageTrigger(__instance);
        }

        [HarmonyPatch(typeof(RuleCalculateDamage), nameof(RuleCalculateDamage.OnTrigger))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RuleCalculateDamage_OnTrigger_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            var replaceWith = AccessTools.Method(typeof(RuleCalculateDamagePatches), nameof(RuleCalculateDamagePatches.OnCalculateDamage));
            var lookFor = AccessTools.Field(typeof(RuleCalculateDamage), nameof(RuleCalculateDamage.CalculatedDamage));
            var match = matcher.SearchForward(x => x.LoadsField(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<RuleCalculateDamagePatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                matcher.Instructions();
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith)
            };
            match = match.Advance(-1).RemoveInstructions(10).Insert(newInstructions);
            Main.GetLogger<RuleCalculateDamagePatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static void OnCalculateDamage(RuleCalculateDamage ruleCalculateDamage)
        {
            if (Main.Multiplayer.IsActive && !PatchesUtils.IsHelperUnit(ruleCalculateDamage.Target.UniqueId))
            {
                var shouldRunOriginalLogic = Main.Rolls.OnBeforeRuleCalculateDamageTrigger(ruleCalculateDamage);
                if (!shouldRunOriginalLogic)
                {
                    return;
                }
            }

            ruleCalculateDamage.CalculatedDamage.InsertRange(0, ruleCalculateDamage.DamageBundle.Select(ruleCalculateDamage.CalculateDamageValue));
        }
    }
}
