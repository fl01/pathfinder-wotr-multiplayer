using HarmonyLib;
using Kingmaker;
using Kingmaker.Designers.EventConditionActionSystem.Actions;
using Kingmaker.ElementsSystem;
using WOTRMultiplayer.Entities.Dialogs;

namespace WOTRMultiplayer.HarmonyPatches.Dialogs
{
    [HarmonyPatch]
    public class ShowMessageBoxPatches
    {
        [HarmonyPatch(typeof(ShowMessageBox), nameof(ShowMessageBox.RunAction))]
        [HarmonyPostfix]
        public static void ShowMessageBox_RunAction_Postfix(ShowMessageBox __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

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

            __instance.OnClose.Actions = [new OnClosedDialogPopupAction(popup), .. __instance.OnClose.Actions];
            Main.Multiplayer.OnDialogPopupShown(popup);
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
