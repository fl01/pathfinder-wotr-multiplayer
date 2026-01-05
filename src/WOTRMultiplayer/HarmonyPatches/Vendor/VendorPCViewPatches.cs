using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
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
        [HarmonyPatch(typeof(VendorPCView), nameof(VendorPCView.BindViewImplementation))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> VendorPCView_BindViewImplementation_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            if (!ReplaceEscManagerSubscription(matcher, target) || !ReplaceDealButtonSubscription(matcher, target))
            {
                Main.GetLogger<VendorPCViewPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return matcher.Instructions();
            }

            Main.GetLogger<VendorPCViewPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
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

        private static bool ReplaceEscManagerSubscription(CodeMatcher matcher, string target)
        {
            var replaceWith = AccessTools.Method(typeof(VendorPCViewPatches), nameof(VendorPCViewPatches.SubscribeToEscHotKey));
            var lookFor = AccessTools.Field(typeof(UIAccess), nameof(UIAccess.EscManager));
            var match = matcher.SearchForward(x => x.LoadsField(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<VendorPCViewPatches>().LogError("ReplaceEscManagerSubscription - Transpiler has not been applied. Target={Target}", target);
                return false;
            }

            match = match.Advance(-3).RemoveInstructions(8);
            var newInstructions = new List<CodeInstruction>
            {
                new (OpCodes.Ldarg_0),
                new (OpCodes.Ldarg_0),
                new (OpCodes.Call, replaceWith)
            };

            match.Insert(newInstructions);
            return true;
        }

        private static bool ReplaceDealButtonSubscription(CodeMatcher matcher, string target)
        {
            var replaceWith = AccessTools.Method(typeof(VendorPCViewPatches), nameof(VendorPCViewPatches.SubscribeToDealButton));
            var match = matcher.Advance(4);
            if (match.Instruction.opcode != OpCodes.Ldarg_0)
            {
                Main.GetLogger<VendorPCViewPatches>().LogError("ReplaceDealButtonSubscription - Transpiler has not been applied. Target={Target}, Instruction={Instruction}", target, match.Instruction);
                return false;
            }

            match = match.RemoveInstructions(8);
            var newInstructions = new List<CodeInstruction>
            {
                new (OpCodes.Ldarg_0),
                new (OpCodes.Ldarg_0),
                new (OpCodes.Call, replaceWith)
            };

            match.Insert(newInstructions);
            return true;
        }

        public static IDisposable SubscribeToEscHotKey(VendorPCView view)
        {
            if (!Main.Multiplayer.IsActive || Main.Multiplayer.CanFullyControlVendorUI())
            {
                var vm = (VendorVM)view.GetViewModel();
                return Game.Instance.UI.EscManager.Subscribe(vm.Close);
            }

            return null;
        }

        public static IDisposable SubscribeToDealButton(VendorPCView view)
        {
            var vm = (VendorVM)view.GetViewModel();
            return vm.IsPossibleDeal.Subscribe(value =>
            {
                view.m_DealButton.Interactable = !Main.Multiplayer.IsActive ? value : value && Main.Multiplayer.CanFullyControlVendorUI();
            });
        }
    }
}
