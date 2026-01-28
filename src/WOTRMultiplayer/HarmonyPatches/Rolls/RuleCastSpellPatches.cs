using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Abilities;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches.Rolls
{
    /// <summary>
    /// SpellFailureRoll + ArcaneSpellFailureRoll
    /// </summary>
    [HarmonyPatch]
    public class RuleCastSpellPatches
    {
        [HarmonyPatch(typeof(RuleCastSpell), nameof(RuleCastSpell.OnTrigger))]
        [HarmonyPostfix]
        public static void RuleCastSpell_OnTrigger_Postfix(RuleCastSpell __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Rolls.OnAfterRuleCastSpellTrigger(__instance);
        }

        [HarmonyPatch(typeof(RuleCastSpell), nameof(RuleCastSpell.OnTrigger))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RuleCastSpell_OnTrigger_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            var spellFailureReplacement = AccessTools.Method(typeof(RuleCastSpellPatches), nameof(RuleCastSpellPatches.RollSpellFailure));
            var lookFor = AccessTools.PropertyGetter(typeof(Kingmaker.RuleSystem.RulebookEvent.Dice), nameof(Kingmaker.RuleSystem.RulebookEvent.Dice.D100));
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<RuleCastSpellPatches>().LogError("Transpiler has been applied (SpellFailureRoll). Target={Target}", target);
                return instructions;
            }

            var spellFailureInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, spellFailureReplacement)
            };
            match = match.RemoveInstruction().Insert(spellFailureInstructions);

            match = match.Advance(spellFailureInstructions.Count).SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<RuleCastSpellPatches>().LogError("Transpiler has been applied (ArcaneSpellFailureRoll). Target={Target}", target);
                return instructions;
            }

            var arcaneFailureReplacement = AccessTools.Method(typeof(RuleCastSpellPatches), nameof(RuleCastSpellPatches.RollSpellFailure));
            var arcaneFailureInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, spellFailureReplacement)
            };
            match = match.RemoveInstruction().Insert(spellFailureInstructions);

            Main.GetLogger<RuleCastSpellPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static RuleRollD100 RollSpellFailure(RuleCastSpell ruleCastSpell)
        {
            return RollD100(ruleCastSpell, true);
        }

        private static RuleRollD100 RollArcaneFailure(RuleCastSpell ruleCastSpell)
        {
            return RollD100(ruleCastSpell, false);
        }

        private static RuleRollD100 RollD100(RuleCastSpell ruleCastSpell, bool isSpellFailure)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return Kingmaker.RuleSystem.RulebookEvent.Dice.D100;
            }

            try
            {
                var shouldRunOriginalLogic = Main.Rolls.OnBeforeRuleCastSpellRoll(ruleCastSpell, isSpellFailure);
                if (!shouldRunOriginalLogic)
                {
                    return isSpellFailure ? ruleCastSpell.SpellFailureRoll : ruleCastSpell.ArcaneSpellFailureRoll;
                }

                return Kingmaker.RuleSystem.RulebookEvent.Dice.D100;
            }
            catch (Exception ex)
            {
                Main.GetLogger<RuleCastSpellPatches>().LogError(ex, "Unable to roll spell cast. IsSpellFailure={IsSpellFailure}", isSpellFailure);
                throw;
            }
        }
    }
}
