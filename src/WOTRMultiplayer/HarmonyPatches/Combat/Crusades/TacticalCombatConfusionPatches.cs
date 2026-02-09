using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.Armies.TacticalCombat.Components;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Services.Random;

namespace WOTRMultiplayer.HarmonyPatches.Combat.Crusades
{
    [HarmonyPatch]
    public class TacticalCombatConfusionPatches
    {
        [HarmonyPatch(typeof(TacticalCombatConfusion), nameof(TacticalCombatConfusion.HandleNextTurn))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TacticalCombatConfusion_HandleNextTurn_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.PropertyGetter(typeof(UnityEngine.Random), nameof(UnityEngine.Random.value));
            var replaceWith = AccessTools.Method(typeof(TacticalCombatConfusionPatches), nameof(TacticalCombatConfusionPatches.RollConfusionEffect));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Is(OpCodes.Newobj, AccessTools.Constructor(typeof(DiceFormula), [typeof(int), typeof(DiceType)])));
            if (match.IsInvalid)
            {
                Main.GetLogger<TacticalCombatConfusionPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            match = match.Advance(-3);
            var labels = match.Instruction.ExtractLabels();
            var newInstructions = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldarg_1).WithLabels(labels),
                new(OpCodes.Ldarg_2),
                new(OpCodes.Call, replaceWith),
            };

            match.RemoveInstructions(7).Insert(newInstructions);
            Main.GetLogger<TacticalCombatConfusionPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static int RollConfusionEffect(int turnNumber, UnitEntityData unit)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return Rulebook.Trigger(new RuleRollDice(unit, new DiceFormula(1, DiceType.D100))).Result;
            }

            try
            {
                var crusadeCombatSeed = Main.Multiplayer.GetCrusadeArmyCombatSeed();
                var identifier = $"{nameof(TacticalCombatConfusion)}:{nameof(RollConfusionEffect)}:{unit.UniqueId}:{turnNumber}:{crusadeCombatSeed}";
                int roll = Main.Multiplayer.ValueGenerator.Range(IdentifierLifetime.Combat, identifier, 1, 101);
                Main.GetLogger<RunActionOnTurnStartPatches>().LogInformation("TacticalCombatConfusion effect has been rolled. UnitId={UnitId}, Roll={Roll}, Identifier={Identifier}", unit.UniqueId, roll, identifier);
                return roll;
            }
            catch (Exception ex)
            {
                Main.GetLogger<RunActionOnTurnStartPatches>().LogError(ex, "Error while rolling unit confusion on turn start. UnitId={UnitId}", unit?.UniqueId);
                throw;
            }
        }
    }
}
