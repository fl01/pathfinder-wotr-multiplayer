using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Designers.EventConditionActionSystem.Actions;
using Kingmaker.UI;
using Kingmaker.UI.MVVM._PCView.Vendor;
using Kingmaker.UI.MVVM._VM.Vendor;
using Microsoft.Extensions.Logging;
using UniRx;

namespace WOTRMultiplayer.HarmonyPatches.Vendor
{
    [HarmonyPatch]
    public class VendorPCViewPatches
    {
        [HarmonyPatch(typeof(StartTrade), nameof(StartTrade.RunAction))]
        [HarmonyPrefix]
        public static void StartTrade_RunAction_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.UIAccessor.CloseAllWindows();
        }

        [HarmonyPatch(typeof(VendorPCView), nameof(VendorPCView.BindViewImplementation))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> VendorPCView_BindViewImplementation_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            var replaceEscWith = AccessTools.Method(typeof(VendorPCViewPatches), nameof(VendorPCViewPatches.SubscribeToEscHotKey));
            var lookForEsc = AccessTools.Method(typeof(EscHotkeyManager), nameof(EscHotkeyManager.Subscribe));
            var match = matcher.SearchForward(x => x.Calls(lookForEsc));
            if (match.IsInvalid)
            {
                Main.GetLogger<VendorPCViewPatches>().LogError("Transpiler has not been applied (EscManager). Target={Target}", target);
                return instructions;
            }

            var newEscInstructions = new List<CodeInstruction>
            {
                new (OpCodes.Ldarg_0),
                new (OpCodes.Call, replaceEscWith)
            };
            match = match.Advance(-6).RemoveInstructions(7).Insert(newEscInstructions);

            var replaceDealWith = AccessTools.Method(typeof(VendorPCViewPatches), nameof(VendorPCViewPatches.SubscribeToDealButton));
            var lookForDeal = AccessTools.Field(typeof(VendorVM), nameof(VendorVM.IsPossibleDeal));
            match = match.SearchForward(x => x.LoadsField(lookForDeal));
            if (match.IsInvalid)
            {
                Main.GetLogger<VendorPCViewPatches>().LogError("Transpiler has not been applied (DealButton). Target={Target}", target);
                return instructions;
            }

            var dealInstructions = new List<CodeInstruction>
            {
                new (OpCodes.Ldarg_0),
                new (OpCodes.Call, replaceDealWith)
            };
            match = match.Advance(-2).RemoveInstructions(7).Insert(dealInstructions);

            Main.GetLogger<VendorPCViewPatches>().LogDebug("Transpiler has been applied (EscManager + DealButton). Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(VendorPCView), nameof(VendorPCView.BindViewImplementation))]
        [HarmonyPostfix]
        public static void VendorPCView_BindViewImplementation_Postfix(VendorPCView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var isFullControl = Main.Multiplayer.CanFullyControlVendorUI();
            if (!isFullControl)
            {
                __instance.m_CloseButton.Interactable = false;
            }
        }

        private static IDisposable SubscribeToEscHotKey(VendorPCView view)
        {
            var subscription = Game.Instance.UI.EscManager.Subscribe(() =>
            {
                if (!Main.Multiplayer.IsActive || Main.Multiplayer.CanFullyControlVendorUI())
                {
                    view.ViewModel.Close();
                }
            });

            return subscription;
        }

        private static IDisposable SubscribeToDealButton(VendorPCView view)
        {
            return view.ViewModel.IsPossibleDeal.Subscribe(value =>
            {
                view.m_DealButton.Interactable = value && (!Main.Multiplayer.IsActive || Main.Multiplayer.CanFullyControlVendorUI());
            });
        }
    }
}
