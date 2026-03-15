using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.UI;
using Kingmaker.UI.MVVM._PCView.Transition;
using Kingmaker.UI.MVVM._VM.Transition;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches.MapObjects
{
    [HarmonyPatch]
    public class TransitionPCViewPatches
    {
        [HarmonyPatch(typeof(TransitionPCView), nameof(TransitionPCView.BindViewImplementation))]
        [HarmonyPostfix]
        public static void TransitionPCView_BindViewImplementation_Postfix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnTransitionMapShown();
        }


        [HarmonyPatch(typeof(TransitionVM), nameof(TransitionVM.Close))]
        [HarmonyPrefix]
        public static void TransitionVM_Close_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnTransitionMapClosed();
        }

        [HarmonyPatch(typeof(TransitionEntryVM), nameof(TransitionEntryVM.Enter))]
        [HarmonyPrefix]
        public static void TransitionEntryVM_Enter_Prefix(TransitionEntryVM __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var entryId = __instance.Entry.AssetGuid.ToString();
            Main.Multiplayer.OnTransitionMapEntryChosen(entryId);
        }


        [HarmonyPatch(typeof(TransitionPCView), nameof(TransitionPCView.BindViewImplementation))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TransitionPCView_BindViewImplementation_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(EscHotkeyManager), nameof(EscHotkeyManager.Subscribe));
            var replaceWith = AccessTools.Method(typeof(TransitionPCViewPatches), nameof(TransitionPCViewPatches.SubscribeEscPress));
            var matcher = new CodeMatcher(instructions);

            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<TransitionPCViewPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith)
            };
            match = match.Advance(-6)
                .RemoveInstructions(7)
                .Insert(newInstructions);

            Main.GetLogger<TransitionPCViewPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static IDisposable SubscribeEscPress(TransitionPCView view)
        {
            var subscription = Game.Instance.UI.EscManager.Subscribe(() =>
            {
                if (!Main.Multiplayer.IsActive || view.m_Parts.Count == 0 || (view.m_Parts.FirstOrDefault(p => view.ViewModel.Map == p.Map)?.Close.Interactable ?? true))
                {
                    view.ViewModel.Close();
                }
            });

            return subscription;
        }
    }
}
