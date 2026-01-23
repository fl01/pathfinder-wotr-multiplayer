using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Armies;
using Kingmaker.Blueprints.Root.Strings;
using Kingmaker.UI;
using Kingmaker.UI.Common;
using Kingmaker.UI.MVVM._PCView.Crusade.ArmyInfo;
using Kingmaker.UI.MVVM._VM.Crusade.ArmyInfo;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Entities.GlobalMap;

namespace WOTRMultiplayer.HarmonyPatches.GlobalMap
{
    [HarmonyPatch]
    public class ArmyCartSetLeaderViewPatches
    {
        [HarmonyPatch(typeof(ArmyCartSetLeaderPCView), nameof(ArmyCartSetLeaderPCView.BindViewImplementation))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> ArmyCartSetLeaderPCView_BindViewImplementation_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(EscHotkeyManager), nameof(EscHotkeyManager.Subscribe));
            var replaceWith = AccessTools.Method(typeof(ArmyCartSetLeaderViewPatches), nameof(ArmyCartSetLeaderViewPatches.SubscribeEnterMessageEscPress));
            var matcher = new CodeMatcher(instructions);

            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<ArmyInfoViewPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith),
            };
            match = match.RemoveInstructions(1).Insert(newInstructions);
            Main.GetLogger<ArmyInfoViewPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(ArmyCartSetLeaderVM), nameof(ArmyCartSetLeaderVM.SetLeader))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> ArmyCartSetLeaderVM_SetLeader_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(UIUtility), nameof(UIUtility.ShowMessageBox));
            var replaceWith = AccessTools.Method(typeof(ArmyCartSetLeaderViewPatches), nameof(ArmyCartSetLeaderViewPatches.OnSetLeader));
            var matcher = new CodeMatcher(instructions);

            var match = matcher.End().Advance(-15);
            if (match.IsInvalid || match.Instruction?.opcode != OpCodes.Ret)
            {
                Main.GetLogger<ArmyInfoViewPatches>().LogError("Transpiler has not been applied. Target={Target}, OpCode={OpCode}", target, match.Instruction?.opcode);
                return instructions;
            }
            match = match.Advance(1);
            var labels = match.Instruction.ExtractLabels();
            var newInstructions = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldarg_0).WithLabels(labels),
                new(OpCodes.Ldarg_1),
                new(OpCodes.Call, replaceWith),
            };
            match = match.RemoveInstructions(14).Insert(newInstructions);
            Main.GetLogger<ArmyInfoViewPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(ArmyCartSetLeaderPCView), nameof(ArmyCartSetLeaderPCView.BindViewImplementation))]
        [HarmonyPostfix]
        public static void ArmyCartSetLeaderPCView_BindViewImplementation_Postfix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnGlobalMapCrusadeArmySetLeaderShown();
        }

        [HarmonyPatch(typeof(ArmyCartSetLeaderView), nameof(ArmyCartSetLeaderView.DestroyViewImplementation))]
        [HarmonyPrefix]
        public static void ArmyCartSetLeaderView_DestroyViewImplementation_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnGlobalMapCrusadeArmySetLeaderClosed();
        }

        [HarmonyPatch(typeof(ArmyCartSetLeaderVM), nameof(ArmyCartSetLeaderVM.OnBuyLeader))]
        [HarmonyPrefix]
        public static void ArmyCartSetLeaderVM_OnBuyLeader_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnGlobalMapCrusadeArmyInfoSetLeaderRecruit();
        }

        [HarmonyPatch(typeof(ArmyCartSetLeaderVM), nameof(ArmyCartSetLeaderVM.OnClearLeader))]
        [HarmonyPrefix]
        public static bool ArmyCartSetLeaderVM_OnClearLeader_Prefix(ArmyCartSetLeaderVM __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            Main.Multiplayer.OnGlobalMapCrusadeArmySetLeaderClear();

            OnClearLeaderRequestPopup(() =>
            {
                __instance.m_State.Data.Leader.DetachFromArmy();
                __instance.UpdateLeaders();
            });
            return false;
        }

        private static void OnSetLeader(ArmyCartSetLeaderVM viewModel, ArmyLeader leader)
        {
            OnClearLeaderRequestPopup(() =>
            {
                ArmyLeader.SwitchArmyLeader(viewModel.m_State.Data, leader);
                viewModel.OnClose();
            });
        }

        private static void OnClearLeaderRequestPopup(Action onAccepted)
        {
            var popup = new NetworkGlobalMapCommonPopup { Type = NetworkGlobalMapCommonPopupType.ClearLeader };
            UIUtility.ShowMessageBox(UIStrings.Instance.CrusadeTexts.ClearCurrentLeaderRequest, MessageModalBase.ModalType.Dialog, type =>
            {
                if (type == MessageModalBase.ButtonType.Yes)
                {
                    Main.Multiplayer.OnGlobalMapCommonPopupAccepted(popup);
                    onAccepted?.Invoke();
                    return;
                }
                Main.Multiplayer.OnGlobalMapCommonPopupDeclined(popup);
            }, null, 0, null, null, null);
            Main.Multiplayer.OnGlobalMapCommonPopupShown(popup);
        }

        private IDisposable SubscribeEnterMessageEscPress(Action action, ArmyCartSetLeaderPCView view)
        {
            return Game.Instance.UI.EscManager.Subscribe(() =>
            {
                if (!Main.Multiplayer.IsActive || view.m_CloseButton.Interactable)
                {
                    action?.Invoke();
                    return;
                }
            });
        }
    }
}
