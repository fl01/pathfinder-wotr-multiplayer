using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.Controllers.Clicks.Handlers;
using Kingmaker.Controllers.Rest;
using Microsoft.Extensions.Logging;
using UnityEngine;
using WOTRMultiplayer.Entities;

namespace WOTRMultiplayer.HarmonyPatches.Rest
{
    [HarmonyPatch]
    public class PlaceRestMarkerHandlerPatches
    {
        /// <summary>
        /// RestHelper type initializer depends on other static fields (UIStrings.Instance.Rest) which can't be initialized early on, so we have to patch this class/method instead
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
                Main.GetLogger<PlaceRestMarkerHandlerPatches>().LogError("Invalid transpiler position. Target={Target}, Position={Position}", target, match.Pos);
                return instructions;
            }

            match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Call, replaceWith),
            };
            match.Insert(newInstructions);
            Main.GetLogger<PlaceRestMarkerHandlerPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        public static void SpawnCampPlace(Vector3 worldPosition)
        {
            var position = new NetworkVector3(worldPosition.x, worldPosition.y, worldPosition.z);
            var canContinue = Main.Multiplayer.OnSpawnCampPlace(position);
            if (canContinue)
            {
                Main.GetLogger<PlaceRestMarkerHandlerPatches>().LogInformation("Spawning camp place. Position={Position}", position);
                RestHelper.SpawnCampPlace(worldPosition);
            }
        }
    }
}
