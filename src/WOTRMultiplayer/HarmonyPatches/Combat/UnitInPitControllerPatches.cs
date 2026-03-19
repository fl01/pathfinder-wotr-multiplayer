using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.Controllers.Units;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.Utility;
using Microsoft.Extensions.Logging;
using UnityEngine;
using WOTRMultiplayer.Extensions;

namespace WOTRMultiplayer.HarmonyPatches.Combat
{
    [HarmonyPatch]
    public class UnitInPitControllerPatches
    {
        [HarmonyPatch(typeof(UnitInPitController), nameof(UnitInPitController.TickOnUnit))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> UnitInPitController_TickOnUnit_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(RoundsExtension), nameof(RoundsExtension.Rounds));
            var replaceWith = AccessTools.Method(typeof(UnitInPitControllerPatches), nameof(UnitInPitControllerPatches.CanTickRound));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<UnitInPitControllerPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Call, replaceWith),
            };
            match = match.Advance(-3).RemoveInstructions(13).Insert(newInstructions);

            var lookForUnitSphere = AccessTools.PropertyGetter(typeof(UnityEngine.Random), nameof(UnityEngine.Random.onUnitSphere));
            var replaceUnitSphereWith = AccessTools.Method(typeof(UnitInPitControllerPatches), nameof(UnitInPitControllerPatches.RollUnitSphere));
            for (var i = 0; i < 1; i++)
            {
                match = match.SearchForward(x => x.Calls(lookForUnitSphere));
                if (match.IsInvalid)
                {
                    Main.GetLogger<UnitInPitControllerPatches>().LogError("Transpiler has not been applied. UnitSphereIndex={UnitSphereIndex}, Target={Target}", i, target);
                    return instructions;
                }
                var labels = match.Instruction.ExtractLabels();
                match = match.RemoveInstruction().Insert(new List<CodeInstruction>()
                {
                    new CodeInstruction(OpCodes.Ldarg_1).WithLabels(labels),
                    new (OpCodes.Call, replaceUnitSphereWith)
                });
            }
            Main.GetLogger<UnitInPitControllerPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static Vector3 RollUnitSphere(UnitEntityData unitEntityData)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return UnityEngine.Random.onUnitSphere;
            }

            try
            {
                var seededContext = Main.Multiplayer.GetSeededContext();
                var identifier = $"{nameof(UnitInPitController)}:{nameof(RollUnitSphere)}:{unitEntityData.UniqueId}:_{seededContext.Id}";
                var pointInSphere = Main.Multiplayer.ValueGenerator.GetRandomUnitSphere(seededContext.Lifetime, identifier);
                Main.GetLogger<UnitInPitControllerPatches>().LogInformation("RandomUnitSphere has been rolled. UnitId={UnitId}, Point={Point}, Lifetime={Lifetime}, Identifier={Identifier}", unitEntityData.UniqueId, pointInSphere, seededContext.Lifetime, identifier);
                return pointInSphere.ToUnityVector3();
            }
            catch (Exception ex)
            {
                Main.GetLogger<UnitInPitControllerPatches>().LogError(ex, "Error while rolling UnitPitController RandomUnitSphere");
                throw;
            }
        }

        private static bool CanTickRound(UnitPartInPit unitPartInPit)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return unitPartInPit.CurrentRoundSeconds >= 1.Rounds().Seconds.TotalSeconds;
            }

            // 3.5seconds is always guaranteed to elapse between rounds, making any possible tick time difference between network players irrelevant
            // while Act3 GhostOracle works completely fine with this approach, the 'Pit problem' might need to be addressed later to get a proper fix
            var canTick = unitPartInPit.CurrentRoundSeconds >= 3.5f;
            return canTick;
        }
    }
}
