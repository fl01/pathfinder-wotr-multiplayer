using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.Armies.TacticalCombat.Components;
using Kingmaker.EntitySystem.Entities;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace WOTRMultiplayer.HarmonyPatches.Combat.Crusades
{
    [HarmonyPatch]
    public class RunActionOnTurnStartPatches
    {
        [HarmonyPatch(typeof(RunActionOnTurnStart), nameof(RunActionOnTurnStart.HandleNextTurn))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RunActionOnTurnStart_HandleNextTurn_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.PropertyGetter(typeof(UnityEngine.Random), nameof(UnityEngine.Random.value));
            var replaceWith = AccessTools.Method(typeof(RunActionOnTurnStartPatches), nameof(RunActionOnTurnStartPatches.RollActionChance));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<RunActionOnTurnStartPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_1),
                new(OpCodes.Ldarg_2),
                new(OpCodes.Call, replaceWith),
            };
            match = match.RemoveInstructions(3).Insert(newInstructions);
            Main.GetLogger<RunActionOnTurnStartPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static float RollActionChance(int turnNumber, UnitEntityData unit)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return UnityEngine.Random.value - Mathf.Epsilon;
            }

            try
            {
                var seededContext = Main.Multiplayer.GetSeededContext();
                var identifier = $"{nameof(RunActionOnTurnStart)}:{nameof(RollActionChance)}:{unit.UniqueId}:{turnNumber}_{seededContext.Id}";
                float roll = Main.Multiplayer.ValueGenerator.Range(seededContext.Lifetime, identifier, 0f, 1f);
                Main.GetLogger<RunActionOnTurnStartPatches>().LogInformation("RunActionOnTurnStart has been rolled. UnitId={UnitId}, Roll={Roll}, Identifier={Identifier}", unit.UniqueId, roll, identifier);
                return roll;
            }
            catch (Exception ex)
            {
                Main.GetLogger<RunActionOnTurnStartPatches>().LogError(ex, "Error while rolling unit action on turn start. UnitId={UnitId}", unit?.UniqueId);
                throw;
            }
        }
    }
}
