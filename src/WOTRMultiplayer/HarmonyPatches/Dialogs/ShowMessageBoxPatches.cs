using System;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Designers.EventConditionActionSystem.Actions;
using Kingmaker.ElementsSystem;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Entities.Dialogs;

namespace WOTRMultiplayer.HarmonyPatches.Dialogs
{
    [HarmonyPatch]
    public class ShowMessageBoxPatches
    {
        [HarmonyPatch(typeof(ShowDialogBox), nameof(ShowDialogBox.RunAction))]
        [HarmonyPostfix]
        public static void ShowDialogBox_RunAction_Postfix(ShowDialogBox __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            try
            {
                var dialogName = Game.Instance.DialogController.Dialog?.name ?? Game.Instance.DialogController.CurrentSpeaker?.CharacterName;
                var cue = Game.Instance.DialogController.CurrentCue?.name;
                if (string.IsNullOrEmpty(dialogName) || string.IsNullOrEmpty(cue))
                {
                    return;
                }

                var popup = new NetworkDialogPopup
                {
                    AreaName = Game.Instance.CurrentlyLoadedArea?.name,
                    DialogName = dialogName,
                    CueName = cue
                };

                __instance.OnCancel.Actions = [new OnClosedDialogPopupAction(popup), .. __instance.OnCancel.Actions];
                __instance.OnAccept.Actions = [new OnAcceptedDialogPopupAction(popup), .. __instance.OnAccept.Actions];
                Main.Multiplayer.OnDialogPopupShown(popup);
            }
            catch (Exception ex)
            {
                Main.GetLogger<ShowMessageBoxPatches>().LogError(ex, "Error while showing dialog popup");
                throw;
            }
        }

        [HarmonyPatch(typeof(ShowMessageBox), nameof(ShowMessageBox.RunAction))]
        [HarmonyPostfix]
        public static void ShowMessageBox_RunAction_Postfix(ShowMessageBox __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            try
            {
                var dialogName = Game.Instance.DialogController.Dialog?.name;
                var cue = Game.Instance.DialogController.CurrentCue?.name;
                if (string.IsNullOrEmpty(dialogName) || string.IsNullOrEmpty(cue))
                {
                    return;
                }

                var popup = new NetworkDialogPopup
                {
                    AreaName = Game.Instance.CurrentlyLoadedArea?.name,
                    DialogName = dialogName,
                    CueName = cue
                };

                // single accept button popup
                __instance.OnClose.Actions = [new OnAcceptedDialogPopupAction(popup), .. __instance.OnClose.Actions];
                Main.Multiplayer.OnDialogPopupShown(popup);
            }
            catch (Exception ex)
            {
                Main.GetLogger<ShowMessageBoxPatches>().LogError(ex, "Error while showing dialog popup");
                throw;
            }
        }

        private class OnAcceptedDialogPopupAction : GameAction
        {
            private readonly NetworkDialogPopup _networkDialogPopup;

            public OnAcceptedDialogPopupAction(NetworkDialogPopup networkDialogPopup)
            {
                _networkDialogPopup = networkDialogPopup;
            }

            public override string GetCaption()
            {
                return string.Empty;
            }

            public override void RunAction()
            {
                Main.Multiplayer.OnDialogPopupAccepted(_networkDialogPopup);
            }
        }

        private class OnClosedDialogPopupAction : GameAction
        {
            private readonly NetworkDialogPopup _networkDialogPopup;

            public OnClosedDialogPopupAction(NetworkDialogPopup networkDialogPopup)
            {
                _networkDialogPopup = networkDialogPopup;
            }

            public override string GetCaption()
            {
                return string.Empty;
            }

            public override void RunAction()
            {
                Main.Multiplayer.OnDialogPopupClosed(_networkDialogPopup);
            }
        }
    }
}
