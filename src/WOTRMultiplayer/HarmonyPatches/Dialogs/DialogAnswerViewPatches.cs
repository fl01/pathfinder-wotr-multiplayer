using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.Dialog.Dialog;
using Owlcat.Runtime.UI.Controls.Other;
using UniRx;

namespace WOTRMultiplayer.HarmonyPatches.Dialogs
{
    [HarmonyPatch]
    public class DialogAnswerViewPatches
    {
        [HarmonyPatch(typeof(DialogAnswerPCView), nameof(DialogAnswerPCView.BindViewImplementation))]
        [HarmonyPostfix]
        public static void DialogAnswerPCView_BindViewImplementation_Postfix(DialogAnswerPCView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            __instance.AddDisposable(__instance.Button.OnRightClickAsObservable().Subscribe(_ =>
            {
                var answerName = __instance.ViewModel.Answer.Value.name;
                var currentCue = __instance.ViewModel.m_DialogController.CurrentCue.name;

                Main.Multiplayer.OnAlternateCueAnswerAction(currentCue, answerName);
            }));
        }
    }
}
