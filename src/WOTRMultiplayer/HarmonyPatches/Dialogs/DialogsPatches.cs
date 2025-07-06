using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints.Root;
using Kingmaker.Controllers.Dialog;
using Kingmaker.DialogSystem.Blueprints;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Localization;
using Kingmaker.UI.MVVM._VM.Dialog.Dialog;
using Kingmaker.View.MapObjects;

namespace WOTRMultiplayer.HarmonyPatches.Dialogs
{
    [HarmonyPatch]
    public class DialogsPatches
    {
        [HarmonyPatch(typeof(DialogController), nameof(DialogController.StartDialog))]
        [HarmonyPrefix]
        public static bool DialogController_StartDialog_Prefix(DialogController __instance, BlueprintDialog dialog, UnitEntityData initiator, UnitEntityData unit, MapObjectView mapObject, LocalizedString customSpeakerName)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.StartDialog(dialog?.name, unit?.UniqueId, initiator?.UniqueId, mapObject?.UniqueId, customSpeakerName?.Key);
            if (!canContinue)
            {
                Game.Instance.Player.Dialog.Scheduled = null;
            }

            return canContinue;
        }

        [HarmonyPatch(typeof(DialogController), nameof(DialogController.SelectAnswer))]
        [HarmonyPrefix]
        public static bool DialogController_SelectAnswer_Prefix(DialogController __instance, BlueprintAnswer answer, UnitEntityData manualUnitSelection)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var isLastAnswer = answer.IsExit() || answer.NextCue.Cues.Count == 0;
            var canContinue = Main.Multiplayer.OnBeforeSelectDialogAnswer(__instance.Dialog.name, __instance.CurrentCue.name, answer.name, isLastAnswer, manualUnitSelection?.UniqueId);
            return canContinue;
        }

        [HarmonyPatch(typeof(DialogController), nameof(DialogController.PlayCue))]
        [HarmonyPostfix]
        public static void DialogController_PlayCue_Postfix(DialogController __instance, BlueprintCueBase cue)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnAfterPlayDialogCue();
        }

        [HarmonyPatch(typeof(DialogVM), nameof(DialogVM.HandleOnCueShow))]
        [HarmonyPostfix] // must be postfix to be able to remove 'continue' hotkey coz it's not configured in prefix yet
        public static void DialogVM_HandleOnCueShow_Postfix(DialogVM __instance, CueShowData data)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var dialogName = Game.Instance.DialogController.Dialog?.name;
            Main.Multiplayer.OnAfterCueShow(dialogName, data.Cue.name, __instance.SystemAnswer.Value != null);
        }
    }
}
