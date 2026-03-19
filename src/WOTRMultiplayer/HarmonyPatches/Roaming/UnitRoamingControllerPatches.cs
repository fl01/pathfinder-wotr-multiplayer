using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.AreaLogic.Cutscenes;
using Kingmaker.Controllers.Units;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.Utility;
using Microsoft.Extensions.Logging;
using UnityEngine;
using WOTRMultiplayer.Extensions;

namespace WOTRMultiplayer.HarmonyPatches.Roaming
{
    [HarmonyPatch]
    public class UnitRoamingControllerPatches
    {
        [HarmonyPatch(typeof(UnitRoamingController), nameof(UnitRoamingController.RollNextPoint))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> UnitRoamingController_RollNextPoint_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.PropertyGetter(typeof(UnityEngine.Random), nameof(UnityEngine.Random.insideUnitCircle));
            var replaceWith = AccessTools.Method(typeof(UnitRoamingControllerPatches), nameof(UnitRoamingControllerPatches.RollNextRoamingPoint));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<UnitRoamingControllerPatches>().LogError("Transpiler has not been applied (NextPoint). Target={Target}", target);
                return instructions;
            }

            var labels = match.Instruction.ExtractLabels();
            var newInstructions = new List<CodeInstruction>
            {
                new (OpCodes.Ldarg_0),
                new (OpCodes.Call, replaceWith),
            };
            match = match.RemoveInstructions(1).Insert(newInstructions);

            var lookForRange = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), [typeof(float), typeof(float)]);
            var replaceRangeWith = AccessTools.Method(typeof(UnitRoamingControllerPatches), nameof(UnitRoamingControllerPatches.RollIdleTime));
            match = matcher.SearchForward(x => x.Calls(lookForRange));
            if (match.IsInvalid)
            {
                Main.GetLogger<UnitRoamingControllerPatches>().LogError("Transpiler has not been applied (IdleTime). Target={Target}", target);
                return instructions;
            }

            var rangeInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceRangeWith),
            };
            match = match.RemoveInstruction().Insert(rangeInstructions);

            var replaceRandomWith = AccessTools.Method(typeof(UnitRoamingControllerPatches), nameof(UnitRoamingControllerPatches.SelectIdleCutscene));
            var lookForRandom = $"{typeof(Cutscene).FullName} {nameof(LinqExtensions.Random)}";
            match = matcher.SearchForward(x => x.opcode == OpCodes.Call && (x.operand?.ToString().Contains(lookForRandom) ?? false));

            if (match.IsInvalid)
            {
                Main.GetLogger<UnitRoamingControllerPatches>().LogError("Invalid transpiler position (IdleCutscene). Target={Target}", target);
                return matcher.Instructions();
            }

            var randomInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceRandomWith),
            };

            match = match.RemoveInstruction().Insert(randomInstructions);
            Main.GetLogger<UnitRoamingControllerPatches>().LogDebug("Transpiler has been applied (NextPoint + IdleTime + IdleCutscene). Target={Target}", target);
            return matcher.Instructions();
        }

        private static Cutscene SelectIdleCutscene(IEnumerable<Cutscene> cutscenes, Func<int, int, int> randomFromRange, UnitPartRoaming roaming)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return cutscenes.Random(randomFromRange);
            }

            try
            {
                var minInclusive = 0;
                var maxExclusive = cutscenes?.Count();
                if (maxExclusive == 0)
                {
                    return null;
                }

                var seededContext = Main.Multiplayer.GetSeededContext();
                var identifier = $"{nameof(UnitRoamingController)}:{nameof(SelectIdleCutscene)}:{roaming.Owner.UniqueId}:{roaming.OriginalPoint}_{seededContext.Id}";
                var cutsceneIndex = Main.Multiplayer.ValueGenerator.Range(seededContext.Lifetime, identifier, minInclusive, maxExclusive.Value);
                var cutscene = cutscenes.ElementAt(cutsceneIndex);
                Main.GetLogger<UnitRoamingControllerPatches>().LogDebug("Unit idle cutscene has been rolled. UnitId={UnitId}, Time={Time}, Identifier={Identifier}", roaming.Owner.UniqueId, cutscene.name, identifier);
                return cutscene;
            }
            catch (Exception ex)
            {
                Main.GetLogger<UnitRoamingControllerPatches>().LogError(ex, "Error rolling unit idle cutscene. UnitId={UnitId}", roaming.Owner.UniqueId);
                throw;
            }
        }

        private static float RollIdleTime(float minInclusive, float maxExclusive, UnitPartRoaming roaming)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return UnityEngine.Random.Range(minInclusive, maxExclusive);
            }

            try
            {
                var seededContext = Main.Multiplayer.GetSeededContext();
                var identifier = $"{nameof(UnitRoamingController)}:{nameof(RollIdleTime)}:{roaming.Owner.UniqueId}:{roaming.OriginalPoint}:{minInclusive}:{maxExclusive}_{seededContext.Id}";
                var idleTime = Main.Multiplayer.ValueGenerator.Range(seededContext.Lifetime, identifier, minInclusive, maxExclusive);
                Main.GetLogger<UnitRoamingControllerPatches>().LogDebug("Unit idle time has been rolled. UnitId={UnitId}, Time={Time}, Identifier={Identifier}", roaming.Owner.UniqueId, idleTime, identifier);
                return idleTime;
            }
            catch (Exception ex)
            {
                Main.GetLogger<UnitRoamingControllerPatches>().LogError(ex, "Error rolling unit idle time. UnitId={UnitId}", roaming.Owner.UniqueId);
                throw;
            }
        }

        private static Vector2 RollNextRoamingPoint(UnitPartRoaming roaming)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return UnityEngine.Random.insideUnitCircle;
            }

            try
            {
                var seededContext = Main.Multiplayer.GetSeededContext();
                var identifier = $"{nameof(UnitRoamingController)}:{nameof(RollNextRoamingPoint)}:{roaming.Owner.UniqueId}:{roaming.OriginalPoint}_{seededContext.Id}";
                var pointInCircle = Main.Multiplayer.ValueGenerator.GetRandomUnitCircle(seededContext.Lifetime, identifier);
                Main.GetLogger<UnitRoamingControllerPatches>().LogDebug("Next unit roaming point has been rolled. UnitId={UnitId}, Point={Point}, Identifier={Identifier}", roaming.Owner.UniqueId, pointInCircle, identifier);
                return pointInCircle.ToUnityVector2();
            }
            catch (Exception ex)
            {
                Main.GetLogger<UnitRoamingControllerPatches>().LogError(ex, "Error rolling next unit roaming point. UnitId={UnitId}", roaming.Owner.UniqueId);
                throw;
            }
        }
    }
}
