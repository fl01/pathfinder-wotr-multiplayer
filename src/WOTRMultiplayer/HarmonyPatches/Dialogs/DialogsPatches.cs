using HarmonyLib;
using Kingmaker;
using Kingmaker.AreaLogic.Etudes;
using Kingmaker.Controllers.Dialog;
using Kingmaker.DialogSystem.Blueprints;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Localization;
using Kingmaker.UI.MVVM._VM.Dialog.Dialog;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.UnitLogic.Interaction;
using Kingmaker.View.MapObjects;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.HarmonyPatches.PubSub;

namespace WOTRMultiplayer.HarmonyPatches.Dialogs
{
    [HarmonyPatch]
    public class DialogsPatches
    {

        [HarmonyPatch(typeof(DialogOnClick), nameof(DialogOnClick.Interact))]
        [HarmonyPostfix]
        public static void DialogOnClick_Interact_Postfix(DialogOnClick __instance, UnitEntityData user, UnitEntityData target, ref UnitCommand.ResultType __result)
        {
            if (!Main.Multiplayer.IsActive || __result != UnitCommand.ResultType.Success)
            {
                return;
            }

            var dialogueId = __instance.Dialog?.name;
            var userId = user?.UniqueId;
            var targetId = target?.UniqueId;

            Main.GetLogger<DialogsPatches>().LogWarning("DialogOnClick. DialogueId={dialogueId}, UserId={userId}, TargetId={targetId}", dialogueId, userId, targetId);
        }


        [HarmonyPatch(typeof(Kingmaker.AreaLogic.Capital.CapitalCompanionLogic.OverrideDialogInteraction), nameof(Kingmaker.AreaLogic.Capital.CapitalCompanionLogic.OverrideDialogInteraction.Interact))]
        [HarmonyPostfix]
        public static void OverrideDialogInteraction_Interact_Postfix(Kingmaker.AreaLogic.Capital.CapitalCompanionLogic.OverrideDialogInteraction __instance, UnitEntityData user, UnitEntityData target, ref UnitCommand.ResultType __result)
        {
            if (!Main.Multiplayer.IsActive || __result != UnitCommand.ResultType.Success)
            {
                return;
            }

            var dialogueId = __instance.Dialog?.name;
            var userId = user?.UniqueId;
            var targetId = target?.UniqueId;

            Main.GetLogger<DialogsPatches>().LogWarning("OverrideDialogInteraction. DialogueId={dialogueId}, UserId={userId}, TargetId={targetId}", dialogueId, userId, targetId);
        }

        [HarmonyPatch(typeof(EtudeBracketOverrideDialog), nameof(EtudeBracketOverrideDialog.Interact))]
        [HarmonyPostfix]
        public static void EtudeBracketOverrideDialog_Interact_Postfix(EtudeBracketOverrideDialog __instance, UnitEntityData user, UnitEntityData target, ref UnitCommand.ResultType __result)
        {
            if (!Main.Multiplayer.IsActive || __result != UnitCommand.ResultType.Success)
            {
                return;
            }

            var dialogueId = __instance.Dialog?.Guid.ToString();
            var userId = user?.UniqueId;
            var targetId = target?.UniqueId;

            Main.GetLogger<DialogsPatches>().LogWarning("EtudeBracketOverrideDialog. DialogueId={dialogueId}, UserId={userId}, TargetId={targetId}", dialogueId, userId, targetId);
        }

        [HarmonyPatch(typeof(SpawnerInteractionDialog), nameof(SpawnerInteractionDialog.Interact))]
        [HarmonyPostfix]
        public static void SpawnerInteractionDialog_Interact_Postfix(SpawnerInteractionDialog __instance, UnitEntityData user, UnitEntityData target, ref UnitCommand.ResultType __result)
        {
            if (!Main.Multiplayer.IsActive || __result != UnitCommand.ResultType.Success)
            {
                return;
            }

            var dialogueId = __instance.Dialog?.name;
            var userId = user?.UniqueId;
            var targetId = target?.UniqueId;

            Main.GetLogger<PubSubPatches>().LogWarning("SpawnerInteractionDialog. DialogueId={dialogueId}, UserId={userId}, TargetId={targetId}", dialogueId, userId, targetId);
        }

        [HarmonyPatch(typeof(DialogController), nameof(DialogController.StartDialogWithUnit))]
        [HarmonyPrefix]
        public static void DialogController_StartDialogWithUnit_Prefix(DialogController __instance, BlueprintDialog dialog, UnitEntityData unit, UnitEntityData initiator)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var dialogueId = dialog?.name;
            var initiatorId = initiator?.UniqueId;
            var targetId = unit?.UniqueId;

            Main.GetLogger<DialogsPatches>().LogWarning("DialogController_StartDialogWithUnit_Prefix. DialogueId={dialogueId}, Initiator={initiatorId}, TargetId={targetId}", dialogueId, initiatorId, targetId);
        }

        [HarmonyPatch(typeof(DialogController), nameof(DialogController.StartDialogWithMapObject))]
        [HarmonyPrefix]
        public static void DialogController_StartDialogWithMapObject_Prefix(DialogController __instance, BlueprintDialog dialog, MapObjectView mapObject, LocalizedString speakerName, UnitEntityData initiator)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var dialogueId = dialog?.name;
            var initiatorId = initiator?.UniqueId;
            var mapObjectName = mapObject?.name;

            Main.GetLogger<DialogsPatches>().LogWarning("DialogController_StartDialogWithMapObject_Prefix. DialogueId={dialogueId}, Initiator={initiatorId}, MapObjectName={targetId}, SpeakerName={speakerName}", dialogueId, initiatorId, mapObjectName, speakerName);
        }

        [HarmonyPatch(typeof(DialogController), nameof(DialogController.StartDialogWithoutTarget))]
        [HarmonyPrefix]
        public static void DialogController_StartDialogWithoutTarget_Prefix(DialogController __instance, BlueprintDialog dialog, LocalizedString speakerName, UnitEntityData initiator)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var dialogueId = dialog?.name;
            var initiatorId = initiator?.UniqueId;

            Main.GetLogger<DialogsPatches>().LogWarning("DialogController_StartDialogWithoutTarget_Prefix. DialogueId={dialogueId}, Initiator={initiatorId}, SpeakerName={speakerName}", dialogueId, initiatorId, speakerName);
        }

        [HarmonyPatch(typeof(DialogController), nameof(DialogController.SelectAnswer))]
        [HarmonyPrefix]
        public static void DialogController_SelectAnswer_Prefix(DialogController __instance, BlueprintAnswer answer, UnitEntityData manualUnitSelection)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            // host - send notification to clients => select answer & continue execution
            // client - skip execution if triggered by user himself, send notification to host => mark answer on host side

            Main.GetLogger<DialogsPatches>().LogWarning("DialogController_SelectAnswer_Prefix. Answer={answer}, ManualUnitSelectionId={manualUnitSelectionId}", answer.name, manualUnitSelection?.UniqueId);
        }

        [HarmonyPatch(typeof(DialogVM), nameof(DialogVM.HandleOnCueShow))]
        [HarmonyPostfix] // must be postfix to be able to remove 'continue' hotkey coz it's not configured in prefix yet
        public static void DialogVM_HandleOnCueShow_Postfix(DialogVM __instance, CueShowData data)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var logger = Main.GetLogger<DialogsPatches>();
            logger.LogInformation("DialogVM_HandleOnCueShow_Postfix - configuring system continue button");
            var dialogName = Game.Instance.DialogController.Dialog?.name;
            Main.Multiplayer.OnAfterCueShow(dialogName, data.Cue.name, __instance.SystemAnswer.HasValue);
        }
    }
}
