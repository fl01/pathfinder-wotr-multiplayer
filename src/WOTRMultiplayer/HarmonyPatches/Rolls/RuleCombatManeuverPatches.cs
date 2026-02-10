using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.Services.Random;
using static Kingmaker.RuleSystem.RulebookEvent;

namespace WOTRMultiplayer.HarmonyPatches.Rolls
{
    [HarmonyPatch]
    public class RuleCombatManeuverPatches
    {
        [HarmonyPatch(typeof(RuleCombatManeuver), nameof(RuleCombatManeuver.OnTrigger))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RuleCombatManeuver_OnTrigger_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(RuleCombatManeuverPatches), nameof(RuleCombatManeuverPatches.TriggerRoll));
            var matcher = new CodeMatcher(instructions);
            var lookFor = AccessTools.PropertyGetter(typeof(Dice), nameof(Dice.D20));
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<RuleCombatManeuverPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith),
            };

            match = match.RemoveInstruction().Insert(newInstructions);
            Main.GetLogger<RuleCombatManeuverPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(RuleCombatManeuver), nameof(RuleCombatManeuver.ApplyManeuver))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RuleDrainEnergy_ApplyManeuver_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(RuleCombatManeuverPatches), nameof(RuleCombatManeuverPatches.ApplyCombatManeuver));
            var matcher = new CodeMatcher(instructions);
            var lookFor = AccessTools.PropertyGetter(typeof(Dice), nameof(Dice.D4));
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<RuleCombatManeuverPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith),
            };

            match = match.RemoveInstruction().Insert(newInstructions);
            Main.GetLogger<RuleCombatManeuverPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static int ApplyCombatManeuver(RuleCombatManeuver ruleCombatManeuver)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return Dice.D4;
            }

            var unitId = ruleCombatManeuver.Initiator.UniqueId;
            var targetUnitId = ruleCombatManeuver.Target?.UniqueId;
            try
            {
                var sessionSeed = Main.Multiplayer.GetSessionSeed();
                var loadedSaveSeed = Main.Multiplayer.GetLoadedSaveSeed();
                var areaSeed = Main.Multiplayer.GetAreaSeed();
                var combatSeed = Main.Multiplayer.GetCombatSeed();
                var combatTurnSeed = Main.Multiplayer.GetCombatTurnSeed();

                var lifetime = combatSeed == 0 ? IdentifierLifetime.Area : IdentifierLifetime.Combat;

                var identifier = $"{nameof(RuleCombatManeuver)}:{nameof(ApplyCombatManeuver)}:{unitId}:{targetUnitId}:{ruleCombatManeuver.InitiatorRoll.Result}:{ruleCombatManeuver.TargetCMD}:{ruleCombatManeuver.Type}:{ruleCombatManeuver.AttackRule?.Weapon.Name}_{sessionSeed}:{loadedSaveSeed}:{areaSeed}:{combatSeed}:{combatTurnSeed}";
                var random = Main.Multiplayer.ValueGenerator.GetRandom(lifetime, identifier);
                var result = new DiceFormula(1, DiceType.D4).Roll(random);
                Main.GetLogger<RuleCombatManeuverPatches>().LogInformation("Combat maneuver random duration has been rolled. Result={Result}, UnitId={UnitId}, TargetUnitId={TargetUnitId}, Type={Type}, IdentifierLifetime={IdentifierLifetime}, Identifier={Identifier}",
                    result, unitId, targetUnitId, ruleCombatManeuver.Type, lifetime, identifier);

                return result;
            }
            catch (Exception ex)
            {
                Main.GetLogger<RuleCombatManeuverPatches>().LogError(ex, "Error while applying combat maneuver. UnitId={UnitId}, TargetUnitId={TargetUnitId}, Type={Type}", unitId, targetUnitId, ruleCombatManeuver.Type);
                throw;
            }
        }

        private static RuleRollD20 TriggerRoll(RuleCombatManeuver ruleCombatManeuver)
        {
            if (Main.Multiplayer.IsActive)
            {
                var shouldRunOriginalLogic = Main.Rolls.OnBeforeRuleCombatManeuverRoll(ruleCombatManeuver);
                if (!shouldRunOriginalLogic)
                {
                    return ruleCombatManeuver.InitiatorRoll;
                }
            }

            var roll = Dice.D20;

            if (Main.Multiplayer.IsActive)
            {
                Main.Rolls.OnAfterRuleCombatManeuverRoll(ruleCombatManeuver, roll);
            }

            return roll;
        }
    }
}
