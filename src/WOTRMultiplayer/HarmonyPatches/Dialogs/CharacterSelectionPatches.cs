using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints.Root;
using Kingmaker.DialogSystem;
using Kingmaker.DialogSystem.Blueprints;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI.BookEvent;
using Kingmaker.UI.MVVM._VM.Dialog.BookEvent;
using Kingmaker.UI.MVVM._VM.Dialog.Dialog;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Entities.Dialogs;
using WOTRMultiplayer.Services.Random;

namespace WOTRMultiplayer.HarmonyPatches.Dialogs
{
    [HarmonyPatch]
    public class CharacterSelectionPatches
    {
        [HarmonyPatch(typeof(BookEventChooseCharacter), nameof(BookEventChooseCharacter.HandleChooseCharacter))]
        [HarmonyPrefix]
        public static bool BookEventChooseCharacter_HandleChooseCharacter_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.CanControlCharacterSelectionWindow();
            return canContinue;
        }

        [HarmonyPatch(typeof(AnswerVM), nameof(AnswerVM.HandleChooseCharacter))]
        [HarmonyPrefix]
        public static bool AnswerVM_HandleChooseCharacter_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.CanControlCharacterSelectionWindow();
            return canContinue;
        }

        [HarmonyPatch(typeof(BookEventVM), nameof(BookEventVM.HandleChooseCharacter))]
        [HarmonyPrefix]
        public static bool BookEventVM_HandleChooseCharacter_Prefix(BlueprintAnswer answer)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var isLastAnswer = answer.IsExit() || answer.NextCue.Cues.Count == 0;
            var networkDialog = new NetworkDialog
            {
                Id = Game.Instance.DialogController.Dialog.AssetGuid.ToString(),
                Name = Game.Instance.DialogController.Dialog.name
            };
            var canContinue = Main.Multiplayer.OnBeforeSelectDialogAnswer(networkDialog, Game.Instance.DialogController.CurrentCue.name, answer.name, isLastAnswer, null);
            return canContinue;
        }

        [HarmonyPatch(typeof(CharacterSelection), nameof(CharacterSelection.SelectUnit))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> CharacterSelection_SelectUnit_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), [typeof(int), typeof(int)]);
            var replaceWith = AccessTools.Method(typeof(CharacterSelectionPatches), nameof(CharacterSelectionPatches.SelectRandomCharacter));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<CharacterSelectionPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldloc_2),
                new(OpCodes.Call, replaceWith),
            };
            match = match.RemoveInstruction().Insert(newInstructions);
            Main.GetLogger<CharacterSelectionPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static int SelectRandomCharacter(int minInclusive, int maxExclusive, UnitEntityData[] units)
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
                var identifier = $"{nameof(CharacterSelection)}:{nameof(SelectRandomCharacter)}:{Game.Instance.CurrentlyLoadedArea.name}:{units.Length}:{Game.Instance.DialogController?.Dialog?.name}:{minInclusive}:{maxExclusive}:{sessionSeed}:{loadedSaveSeed}:{areaSeed}";
                int index = Main.Multiplayer.ValueGenerator.Range(IdentifierLifetime.Area, identifier, minInclusive, maxExclusive);
                var unit = units[index];
                Main.GetLogger<CueSelectionPatches>().LogInformation("Dialog random unit has been selected. Index={Index}, UnitName={UnitName}, MinRange={MinRange}, MaxRange={MaxRange}, Identifier={Identifier}", index, unit.CharacterName, minInclusive, maxExclusive, identifier);
                return index;
            }
            catch (System.Exception ex)
            {
                Main.GetLogger<CueSelectionPatches>().LogError(ex, "Unable to select random dialog character");
                throw;
            }
        }
    }
}
