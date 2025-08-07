using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.Controllers.Clicks.Handlers;
using Kingmaker.Controllers.Rest;
using Microsoft.Extensions.Logging;
using UnityEngine;
using WOTRMultiplayer.MP.Entities;

namespace WOTRMultiplayer.HarmonyPatches.Rest
{
    [HarmonyPatch]
    public class PlaceRestMarkerHandlerPatches
    {
        /// <summary>
        /// the only reason why this is a transpiler of PlaceRestMarkerHandler because RestHelper throws TypeInitialization exception on attempt to patch
        /// (thank you for accessing UIStrings.Instance.Rest in a static field)
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        [HarmonyPatch(typeof(PlaceRestMarkerHandler), nameof(PlaceRestMarkerHandler.OnClick))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> PlaceRestMarkerHandler_OnClick_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(PlaceRestMarkerHandlerPatches), nameof(SpawnCampPlace));
            var lookFor = AccessTools.Method(typeof(RestHelper), nameof(RestHelper.SpawnCampPlace));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            if (match.IsInvalid)
            {
                Main.GetLogger<PlaceRestMarkerHandlerPatches>().LogError("Invalid transpiler position. Target={target}, Pos={pos}", target, match.Pos);
                return instructions;
            }

            match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Call, replaceWith),
            };
            match.Insert(newInstructions);
            Main.GetLogger<PlaceRestMarkerHandlerPatches>().LogInformation("Transpiler has been applied. Target={target}", target);
            return matcher.Instructions();
        }

        public static void SpawnCampPlace(Vector3 worldPosition)
        {
            var position = new NetworkVector3(worldPosition.x, worldPosition.y, worldPosition.z);
            var canContinue = Main.Multiplayer.OnSpawnCampPlace(position);
            if (!canContinue)
            {
                return;
            }

            RestHelper.SpawnCampPlace(worldPosition);
        }
    }
}
