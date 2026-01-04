using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches.Rolls
{
    [HarmonyPatch]
    public class RuleConcealmentPatches
    {
        [HarmonyPatch(typeof(RuleConcealmentCheck), nameof(RuleConcealmentCheck.OnTrigger))]
        [HarmonyPostfix]
        public static void RuleConcealmentCheck_OnTrigger_Postfix(RuleConcealmentCheck __instance)
        {
            if (!Main.Multiplayer.IsActive
                || PatchesUtils.IsHelperUnit(__instance.Initiator.UniqueId)
                || PatchesUtils.IsHelperUnit(__instance.Target?.UniqueId))
            {
                return;
            }

            Main.Rolls.OnAfterRuleConcealmentCheckTrigger(__instance);
        }

        [HarmonyPatch(typeof(RuleConcealmentCheck), nameof(RuleConcealmentCheck.OnTrigger))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RuleConcealmentCheck_OnTrigger_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            var replaceWith = AccessTools.Method(typeof(RuleConcealmentPatches), nameof(RuleConcealmentPatches.ConcealmentRollD100));
            var lookFor = AccessTools.PropertyGetter(typeof(RuleConcealmentCheck), nameof(RuleConcealmentCheck.Roll));
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<RuleConcealmentPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                matcher.Instructions();
            }

            match = match.RemoveInstructions(2);

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Call, replaceWith)
            };

            match.Insert(newInstructions);
            Main.GetLogger<RuleConcealmentPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);

            return matcher.Instructions();
        }

        public static RuleRollD100 ConcealmentRollD100(RuleConcealmentCheck ruleConcealmentCheck)
        {
            if (!Main.Multiplayer.IsActive
                || PatchesUtils.IsHelperUnit(ruleConcealmentCheck.Initiator.UniqueId)
                || PatchesUtils.IsHelperUnit(ruleConcealmentCheck.Target?.UniqueId))
            {
                return Rulebook.Trigger<RuleRollD100>(ruleConcealmentCheck.Roll);
            }

            var shouldRunOriginalLogic = Main.Rolls.OnBeforeRuleConcealmentCheckTrigger(ruleConcealmentCheck);
            if (!shouldRunOriginalLogic)
            {
                return ruleConcealmentCheck.Roll;
            }

            return Rulebook.Trigger<RuleRollD100>(ruleConcealmentCheck.Roll);
        }
    }
}
