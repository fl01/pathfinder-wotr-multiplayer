using System;
using System.Linq;
using System.Threading;
using HarmonyLib;
using Kingmaker;
using Kingmaker.AreaLogic.Cutscenes.Commands;
using Kingmaker.Blueprints.Root;
using Kingmaker.Controllers.Dialog;
using Kingmaker.Designers.EventConditionActionSystem.Actions;
using Kingmaker.DialogSystem.Blueprints;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Localization;
using Kingmaker.UI.MVVM._VM.Dialog.Dialog;
using Kingmaker.View.MapObjects;
using WOTRMultiplayer.Entities.Dialogs;

namespace WOTRMultiplayer.HarmonyPatches.Dialogs
{
    [HarmonyPatch]
    public class DialogsPatches
    {
        private static readonly AsyncLocal<bool> _isScriptedDialog = new();

        [HarmonyPatch(typeof(StartDialog), nameof(StartDialog.RunAction))]
        [HarmonyPrefix]
        public static void StartDialog_RunAction_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            _isScriptedDialog.Value = true;
        }

        [HarmonyPatch(typeof(CommandStartDialog), nameof(CommandStartDialog.OnRun))]
        [HarmonyPrefix]
        public static void CommandStartDialog_OnRun_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            _isScriptedDialog.Value = true;
        }

        [HarmonyPatch(typeof(DialogController), nameof(DialogController.StartDialog))]
        [HarmonyPrefix]
        public static bool DialogController_StartDialog_Prefix(BlueprintDialog dialog, UnitEntityData initiator, UnitEntityData unit, MapObjectView mapObject, LocalizedString customSpeakerName)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var networkDialog = new NetworkDialog
            {
                Id = dialog.AssetGuid.ToString(),
                Name = dialog.name,
                InitiatorUnitId = initiator?.UniqueId,
                MapObjectId = mapObject?.UniqueId,
                SpeakerKey = customSpeakerName?.Key,
                TargetUnitId = unit?.UniqueId,
                IsScripted = _isScriptedDialog.Value
            };
            _isScriptedDialog.Value = false;

            var canContinue = Main.Multiplayer.StartDialog(networkDialog);
            if (!canContinue
                || Game.Instance.Player.Dialog.Scheduled != null && string.Equals(dialog.AssetGuid.ToString(), Game.Instance.Player.Dialog.Scheduled.Dialog.AssetGuid.ToString(), StringComparison.OrdinalIgnoreCase))
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

            var isLastAnswer = answer.IsExit()
                || answer.NextCue.Cues.Count == 0
                || answer.NextCue.Cues.Count == 1 && answer.NextCue.GetAllNextCues().All(x => x.ShowOnce && !x.ShowOnceCurrentDialog);

            var networkDialog = new NetworkDialog
            {
                Id = __instance.Dialog.AssetGuid.ToString(),
                Name = __instance.Dialog.name
            };
            var canContinue = Main.Multiplayer.OnBeforeSelectDialogAnswer(networkDialog, __instance.CurrentCue.name, answer.name, isLastAnswer, manualUnitSelection?.UniqueId);
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

            if (__instance.PlayingBookPage)
            {
                var dialog = Game.Instance.DialogController.Dialog;
                var networkDialog = new NetworkDialog
                {
                    Id = dialog.AssetGuid.ToString(),
                    Name = dialog.name
                };
                Main.Multiplayer.OnAfterCueShow(networkDialog, cue.name, false);
                return;
            }

            Main.Multiplayer.OnAfterPlayDialogCue();
        }

        [HarmonyPatch(typeof(DialogVM), nameof(DialogVM.HandleOnCueShow))]
        [HarmonyPostfix]
        public static void DialogVM_HandleOnCueShow_Postfix(DialogVM __instance, CueShowData data)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var dialog = Game.Instance.DialogController.Dialog;
            var networkDialog = new NetworkDialog
            {
                Id = dialog.AssetGuid.ToString(),
                Name = dialog.name
            };
            Main.Multiplayer.OnAfterCueShow(networkDialog, data.Cue.name, __instance.SystemAnswer.Value != null);
        }
    }
}
