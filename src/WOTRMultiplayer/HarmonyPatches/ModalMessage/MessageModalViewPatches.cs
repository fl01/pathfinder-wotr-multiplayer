using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.UI;
using Kingmaker.UI.MVVM._PCView.Common.MessageModal;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches.ModalMessage
{
    [HarmonyPatch]
    public class MessageModalViewPatches
    {
        [HarmonyPatch(typeof(MessageModalPCView), nameof(MessageModalPCView.BindViewImplementation))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> MessageModalPCView_BindViewImplementation_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(EscHotkeyManager), nameof(EscHotkeyManager.Subscribe));
            var replaceWith = AccessTools.Method(typeof(MessageModalViewPatches), nameof(MessageModalViewPatches.SubscribeMessageEscPress));
            var matcher = new CodeMatcher(instructions);

            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<MessageModalViewPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith)
            };
            match = match.Advance(-6).RemoveInstructions(7).Insert(newInstructions);

            Main.GetLogger<MessageModalViewPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static IDisposable SubscribeMessageEscPress(MessageModalPCView view)
        {
            return Game.Instance.UI.EscManager.Subscribe(() =>
            {
                if (!Main.Multiplayer.IsActive || view.m_DeclineButton.Interactable)
                {
                    view.ViewModel.OnDeclinePressed();
                }
            });
        }
    }
}
