using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.Controllers.Units;
using Kingmaker.EntitySystem.Entities;
using Microsoft.Extensions.Logging;
using UnityEngine;
using WOTRMultiplayer.Services.Random;

namespace WOTRMultiplayer.HarmonyPatches.Combat
{
    [HarmonyPatch]
    public class UnitFearControllerPatches
    {
        [HarmonyPatch(typeof(UnitFearController), nameof(UnitFearController.GetPointForMove))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> UnitFearController_GetPointForMove_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), [typeof(int), typeof(int)]);
            var replaceWith = AccessTools.Method(typeof(UnitFearControllerPatches), nameof(UnitFearControllerPatches.SelectPointToMove));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<UnitFearControllerPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldloc_0),
                new(OpCodes.Call, replaceWith),
            };
            match = match.RemoveInstructions(2).Insert(newInstructions);
            Main.GetLogger<UnitFearControllerPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private class CompilerGeneratedMovementData
        {
            // order must match order of the anonymous class fields
            public UnitEntityData unit = null;

            public bool IsValid()
            {
                return unit is not null and UnitEntityData;
            }
        }

        private static Vector3 SelectPointToMove(List<Vector3> points, int minInclusive, int maxExclusive, CompilerGeneratedMovementData movementData)
        {
            if (!Main.Multiplayer.IsActive || !movementData.IsValid())
            {
                return points.ElementAt(UnityEngine.Random.Range(minInclusive, maxExclusive));
            }

            var sessionSeed = Main.Multiplayer.GetSessionSeed();
            var combatSeed = Main.Multiplayer.GetCombatSeed();
            var combatTurnSeed = Main.Multiplayer.GetCombatTurnSeed();
            var identifier = $"{nameof(UnitFearController)}:{nameof(SelectPointToMove)}:{movementData.unit.UniqueId}:{sessionSeed}:{combatSeed}:{combatTurnSeed}";

            var index = Main.Multiplayer.ValueGenerator.Range(SeedLifetime.CombatTurn, identifier, minInclusive, maxExclusive);
            var point = points.ElementAt(index);
            Main.GetLogger<UnitFearControllerPatches>().LogInformation("Fear move point has been selected. UnitId={UnitId}, Point={Point}, Index={Index}, TurnSeed={TurnSeed}", movementData.unit.UniqueId, point, index, combatTurnSeed);
            return point;
        }
    }
}
