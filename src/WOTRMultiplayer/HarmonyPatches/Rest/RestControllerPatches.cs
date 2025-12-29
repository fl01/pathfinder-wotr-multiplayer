using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.BarkBanters;
using Kingmaker.Controllers.Rest;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Entities.Rest;

namespace WOTRMultiplayer.HarmonyPatches.Rest
{

    [HarmonyPatch]
    public class RestControllerPatches
    {
        [HarmonyPatch(typeof(RestController), nameof(RestController.SkipPhase))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RestController_SkipPhase_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(RestControllerPatches), nameof(RestControllerPatches.OnInterruptBanterBark));
            var lookFor = AccessTools.Method(typeof(BarkBanterPlayer), nameof(BarkBanterPlayer.InterruptBark));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            if (match.IsInvalid)
            {
                Main.GetLogger<PlaceRestMarkerHandlerPatches>().LogError("Invalid transpiler position. Target={Target}, Position={Position}", target, match.Pos);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith),
            };
            match.Advance(-1).Insert(newInstructions);
            Main.GetLogger<PlaceRestMarkerHandlerPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        public static void OnInterruptBanterBark(RestController restController)
        {
            var banterPlayer = restController.m_BanterPlayer;
            if (!Main.Multiplayer.IsActive || banterPlayer == null || banterPlayer.m_NextEntryIndex == 0)
            {
                return;
            }

            var currentEntryIndex = banterPlayer.m_NextEntryIndex - 1;
            var currentBark = banterPlayer.m_Entries[currentEntryIndex];
            var networkBanter = new NetworkRestBanter
            {
                Key = currentBark.Text.Key,
                SpeakerUnitId = currentBark.Speaker.UniqueId
            };

            Main.Multiplayer.OnInterrupRestBanterBark(networkBanter);
        }
    }
}
