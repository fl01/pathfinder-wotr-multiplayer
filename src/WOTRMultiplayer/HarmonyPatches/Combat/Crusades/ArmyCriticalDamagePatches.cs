using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.Armies.TacticalCombat.Components;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Extensions;

namespace WOTRMultiplayer.HarmonyPatches.Combat.Crusades
{
    [HarmonyPatch]
    public class ArmyCriticalDamagePatches
    {
        [HarmonyPatch(typeof(ArmyCriticalDamage), nameof(ArmyCriticalDamage.OnEventAboutToTrigger))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> ArmyCriticalDamage_OnEventAboutToTrigger_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(ArmyCriticalDamagePatches), nameof(ArmyCriticalDamagePatches.RollCriticalChance));
            var matcher = new CodeMatcher(instructions);
            var lookFor = $"{typeof(RuleRollD20).FullName} {nameof(Rulebook.Trigger)}";
            var match = matcher.SearchForward(x => x.opcode == OpCodes.Call && (x.operand?.ToString().Contains(lookFor) ?? false));
            if (match.IsInvalid)
            {
                Main.GetLogger<ArmyCriticalDamagePatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith),
            };
            match = match.RemoveInstruction().Insert(newInstructions);

            Main.GetLogger<ArmyCriticalDamagePatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static RuleRollD20 RollCriticalChance(RuleRollD20 ruleRollD20, ArmyCriticalDamage armyCriticalDamage)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return Rulebook.Trigger<RuleRollD20>(ruleRollD20);
            }

            try
            {
                var seededContext = Main.Multiplayer.GetSeededContext();
                var unitId = ruleRollD20.Initiator?.UniqueId ?? armyCriticalDamage.Owner.UniqueId;
                var identifier = $"{nameof(ArmyCriticalDamage)}:{nameof(RollCriticalChance)}:{unitId}_{seededContext.Id}";
                var random = Main.Multiplayer.ValueGenerator.GetRandom(seededContext.Lifetime, identifier);
                var result = ruleRollD20.DiceFormula.Roll(random);
                Main.GetLogger<ArmyCriticalDamagePatches>().LogInformation("ArmyCriticalDamage has been rolled. UnitId={UnitId}, Roll={Roll}, Identifier={Identifier}", unitId, result, identifier);
                ruleRollD20.m_Triggered = true;
                ruleRollD20.m_Result = result;
                return ruleRollD20;
            }
            catch (Exception ex)
            {
                Main.GetLogger<ArmyCriticalDamagePatches>().LogError(ex, "Error while rolling crusade army critical chance. UnitId={UnitId}", ruleRollD20.Initiator?.UniqueId);
                throw;
            }
        }
    }
}
