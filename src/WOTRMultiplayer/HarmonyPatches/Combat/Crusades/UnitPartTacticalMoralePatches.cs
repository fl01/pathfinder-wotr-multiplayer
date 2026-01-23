using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Armies.TacticalCombat.Parts;
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
            Main.GetLogger<UnitPartTacticalMoralePatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static int RollSuccess(int minInclusive, int maxExclusive, UnitPartTacticalMorale unitPartTacticalMorale)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return UnityEngine.Random.Range(minInclusive, maxExclusive);
            }
            try
            {

                var turnNumber = Game.Instance.TacticalCombat?.Data?.Turn?.Number ?? -1;
                var unit = unitPartTacticalMorale.Owner;
                var identifier = $"{nameof(UnitPartTacticalMorale)}:{unit.UniqueId}:{turnNumber}:{unitPartTacticalMorale.Morale}:{unitPartTacticalMorale.m_PositiveMod}:{unitPartTacticalMorale.m_PositiveChanceBonus}";
                var roll = Main.Multiplayer.ValueGenerator.Range(SeedLifetime.Combat, identifier, minInclusive, maxExclusive);
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
