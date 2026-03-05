using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Armies.Blueprints;
using Kingmaker.Armies.TacticalCombat.Parts;
using Kingmaker.Blueprints.Facts;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Services.Random;

namespace WOTRMultiplayer.HarmonyPatches.Combat.Crusades
{
    [HarmonyPatch]
    public class UnitPartTacticalMoralePatches
    {
        [HarmonyPatch(typeof(UnitPartTacticalMorale), nameof(UnitPartTacticalMorale.IsSuccess))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> UnitPartTacticalMorale_IsSuccess_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), [typeof(int), typeof(int)]);
            var replaceWith = AccessTools.Method(typeof(UnitPartTacticalMoralePatches), nameof(UnitPartTacticalMoralePatches.RollSuccess));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<UnitPartTacticalMoralePatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith),
            };
            match = match.RemoveInstruction().Insert(newInstructions);
            Main.GetLogger<UnitPartTacticalMoralePatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(UnitPartTacticalMorale), nameof(UnitPartTacticalMorale.CheckNegative))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> UnitPartTacticalMorale_CheckNegative_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(MoraleRoot), nameof(MoraleRoot.GetRandomNegativeFact));
            var replaceWith = AccessTools.Method(typeof(UnitPartTacticalMoralePatches), nameof(UnitPartTacticalMoralePatches.RollRandomNegativeFact));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<UnitPartTacticalMoralePatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith),
            };
            match = match.RemoveInstruction().Insert(newInstructions);
            Main.GetLogger<UnitPartTacticalMoralePatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static BlueprintUnitFact RollRandomNegativeFact(MoraleRoot moraleRoot, UnitPartTacticalMorale unitPartTacticalMorale)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return moraleRoot.GetRandomNegativeFact();
            }

            try
            {

                if (moraleRoot.m_NegativeFacts.Length == 0)
                {
                    return null;
                }

                var turnNumber = Game.Instance.TacticalCombat?.Data?.Turn?.Number ?? -1;
                var unit = unitPartTacticalMorale.Owner;
                var minInclusive = 0;
                var maxExclusive = moraleRoot.m_NegativeFacts.Length;
                var identifier = $"{nameof(UnitPartTacticalMorale)}:{unit.UniqueId}:{turnNumber}:{unitPartTacticalMorale.Morale}:{unitPartTacticalMorale.m_NegativeMod}:{unitPartTacticalMorale.m_NegativeChanceBonus}:{minInclusive}:{maxExclusive}";
                var index = Main.Multiplayer.ValueGenerator.Range(IdentifierLifetime.Area, identifier, minInclusive, maxExclusive);
                var fact = moraleRoot.m_NegativeFacts[index].Get();
                Main.GetLogger<TacticalCombatControllerPatches>().LogInformation("Tactical combat negative fact has been rolled. UnitId={UnitId}, Index={Index}, FactName={FactName}, Identifier={Identifier}, MinInclusive={MinInclusive}, MaxExclusive={MaxExclusive}", unitPartTacticalMorale.Owner.UniqueId, index, fact.name, identifier, minInclusive, maxExclusive);
                return fact;
            }
            catch (Exception ex)
            {
                Main.GetLogger<TacticalCombatControllerPatches>().LogError(ex, "Error while rolling unit negative fact. UnitId={UnitId}", unitPartTacticalMorale.Owner?.UniqueId);
                throw;
            }
        }

        private static int RollSuccess(int minInclusive, int maxExclusive, UnitPartTacticalMorale unitPartTacticalMorale)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return UnityEngine.Random.Range(minInclusive, maxExclusive);
            }

            try
            {

                var sessionSeed = Main.Multiplayer.GetSessionSeed();
                var loadedSaveSeed = Main.Multiplayer.GetLoadedSaveSeed();
                var armyCombatTurnSeed = Main.Multiplayer.GetCrusadeArmyCombatSeed();

                var turnNumber = Game.Instance.TacticalCombat?.Data?.Turn?.Number ?? -1;
                var unit = unitPartTacticalMorale.Owner;
                var identifier = $"{nameof(UnitPartTacticalMorale)}:{nameof(RollSuccess)}:{unit.UniqueId}:{turnNumber}:{unitPartTacticalMorale.Morale}:{unitPartTacticalMorale.m_PositiveMod}:{unitPartTacticalMorale.m_PositiveChanceBonus}_{sessionSeed}:{loadedSaveSeed}:{armyCombatTurnSeed}";
                var roll = Main.Multiplayer.ValueGenerator.Range(IdentifierLifetime.Combat, identifier, minInclusive, maxExclusive);
                Main.GetLogger<TacticalCombatControllerPatches>().LogInformation("Tactical combat morale has been rolled. UnitId={UnitId}, Roll={Roll}, Identifier={Identifier}, MinInclusive={MinInclusive}, MaxExclusive={MaxExclusive}", unitPartTacticalMorale.Owner.UniqueId, roll, identifier, minInclusive, maxExclusive);
                return roll;
            }
            catch (Exception ex)
            {
                Main.GetLogger<TacticalCombatControllerPatches>().LogError(ex, "Error while rolling unit morale. UnitId={UnitId}", unitPartTacticalMorale.Owner?.UniqueId);
                throw;
            }
        }
    }
}
