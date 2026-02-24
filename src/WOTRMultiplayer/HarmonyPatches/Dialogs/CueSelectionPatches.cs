using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.DialogSystem;
using Kingmaker.DialogSystem.Blueprints;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Services.Random;

namespace WOTRMultiplayer.HarmonyPatches.Dialogs
{
    [HarmonyPatch]
    public class CueSelectionPatches
    {
        [HarmonyPatch(typeof(CueSelection), nameof(CueSelection.Select))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> CueSelection_Select_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), [typeof(int), typeof(int)]);
            var replaceWith = AccessTools.Method(typeof(CueSelectionPatches), nameof(CueSelectionPatches.SelectRandomDialogCue));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<CueSelectionPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldloc_0),
                new(OpCodes.Call, replaceWith),
            };
            match = match.RemoveInstruction().Insert(newInstructions);
            Main.GetLogger<CueSelectionPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static int SelectRandomDialogCue(int minInclusive, int maxExclusive, List<BlueprintCueBase> cues)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return UnityEngine.Random.Range(minInclusive, maxExclusive);
            }

            try
            {
                var sessionSeed = Main.Multiplayer.GetSessionSeed();
                var loadedSaveSeed = Main.Multiplayer.GetLoadedSaveSeed();
                var areaSeed = Main.Multiplayer.GetAreaSeed();
                var identifier = $"{nameof(CueSelection)}:{nameof(SelectRandomDialogCue)}:{Game.Instance.CurrentlyLoadedArea.name}:{cues.Count}:{Game.Instance.DialogController?.Dialog?.name}:{minInclusive}:{maxExclusive}:{sessionSeed}:{loadedSaveSeed}:{areaSeed}";
                int index = Main.Multiplayer.ValueGenerator.Range(IdentifierLifetime.Area, identifier, minInclusive, maxExclusive);
                var cue = cues[index];
                Main.GetLogger<CueSelectionPatches>().LogInformation("Dialog cue has been selected. Index={Index}, CueName={CueName}, MinRange={MinRange}, MaxRange={MaxRange}, Identifier={Identifier}", index, cue.name, minInclusive, maxExclusive, identifier);
                return index;
            }
            catch (System.Exception ex)
            {
                Main.GetLogger<CueSelectionPatches>().LogError(ex, "Unable to select dialog cue");
                throw;
            }
        }
    }
}
