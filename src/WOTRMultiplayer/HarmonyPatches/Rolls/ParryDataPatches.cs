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
    public class ParryDataPatches
    {
        [HarmonyPatch(typeof(RuleAttackRoll.ParryData), nameof(RuleAttackRoll.ParryData.Trigger))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> ParryData_OnTrigger_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            var replaceWith = AccessTools.Method(typeof(ParryDataPatches), nameof(ParryDataRollD20));
            var lookFor = AccessTools.PropertyGetter(typeof(Dice), nameof(Dice.D20));
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<ParryDataPatches>().LogError("Transpiler has not been applied. Target={Target}, Position={Position}", target, match.Pos);
                matcher.Instructions();
            }

            match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith)
            };

            match.Insert(newInstructions);
            Main.GetLogger<ParryDataPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);

            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(RuleAttackRoll.ParryData), nameof(RuleAttackRoll.ParryData.Trigger))]
        [HarmonyPostfix]
        public static void ParryData_Trigger_Postfix(RuleAttackRoll.ParryData __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Rolls.OnAfterParryDataTrigger(__instance);
        }

        public static RuleRollD20 ParryDataRollD20(RuleAttackRoll.ParryData parryData)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return Dice.D20;
            }

            var shouldRunOriginalLogic = Main.Rolls.OnBeforeParryDataTrigger(parryData);
            if (!shouldRunOriginalLogic)
            {
                return parryData.Roll;
            }

            return Dice.D20;
        }
    }
}
