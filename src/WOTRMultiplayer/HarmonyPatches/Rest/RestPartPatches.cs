using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.View.MapObjects;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches.Rest
{
    [HarmonyPatch]
    public class RestPartPatches
    {
        [HarmonyPatch(typeof(RestPart), nameof(RestPart.OnInteract))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RestPart_OnInteract_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var replaceWith = AccessTools.Method(typeof(CommonTranspilerReplacements), nameof(CommonTranspilerReplacements.GetPartyCharactersForGroupCommand));
            var lookFor = AccessTools.Method(typeof(Player), nameof(Player.GetPartyCharactersForGroupCommand));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));

            if (match.IsInvalid)
            {
                Main.GetLogger<RestPartPatches>().LogError("Invalid transpiler position. Target={Target}, Position={Position}", target, match.Pos);
                return instructions;
            }

            match.RemoveInstruction();
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Call, replaceWith),
            };
            match.Insert(newInstructions);
            Main.GetLogger<RestPartPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }
    }
}
