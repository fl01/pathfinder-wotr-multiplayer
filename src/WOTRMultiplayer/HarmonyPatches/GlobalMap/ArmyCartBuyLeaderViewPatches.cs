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
using Microsoft.Extensions.Logging;
using UniRx;
using WOTRMultiplayer.Entities.GlobalMap;

namespace WOTRMultiplayer.HarmonyPatches.GlobalMap
{
    [HarmonyPatch]
    public class ArmyCartBuyLeaderViewPatches
    {
        [HarmonyPatch(typeof(ArmyCartBuyLeaderView), nameof(ArmyCartBuyLeaderView.BindViewImplementation))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> ArmyCartBuyLeaderView_BindViewImplementation_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);

            var match = matcher.End().Advance(-9);
            if (match.IsInvalid || match.Opcode != OpCodes.Ldarg_0)
            {
                Main.GetLogger<ArmyInfoViewPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            match.RemoveInstructions(9);
            Main.GetLogger<ArmyInfoViewPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(ArmyCartBuyLeaderView), nameof(ArmyCartBuyLeaderView.BindViewImplementation))]
        [HarmonyPostfix]
        public static void ArmyCartBuyLeaderView_BindViewImplementation_Postfix(ArmyCartBuyLeaderView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            __instance.AddDisposable(__instance.ViewModel.OnRecruitCommand.Subscribe(leader => OnRecruitCommand(leader, __instance)));
        }

        [HarmonyPatch(typeof(ArmyCartBuyLeaderPCView), nameof(ArmyCartBuyLeaderPCView.BindViewImplementation))]
        [HarmonyPostfix]
        public static void ArmyCartBuyLeaderPCView_BindViewImplementation_Postfix(ArmyCartBuyLeaderPCView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnGlobalMapCrusadeArmyBuyLeaderShown();
        }

        [HarmonyPatch(typeof(ArmyCartBuyLeaderView), nameof(ArmyCartBuyLeaderView.DestroyViewImplementation))]
        [HarmonyPrefix]
        public static void ArmyCartBuyLeaderView_DestroyViewImplementation_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnGlobalMapCrusadeArmyBuyLeaderClosed();
        }

        [HarmonyPatch(typeof(ArmyCartBuyLeaderPCView), nameof(ArmyCartBuyLeaderPCView.BindViewImplementation))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> ArmyCartBuyLeaderPCView_BindViewImplementation_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(EscHotkeyManager), nameof(EscHotkeyManager.Subscribe));
            var replaceWith = AccessTools.Method(typeof(ArmyCartBuyLeaderViewPatches), nameof(ArmyCartBuyLeaderViewPatches.SubscribeEnterMessageEscPress));
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

        private IDisposable SubscribeEnterMessageEscPress(Action action, ArmyCartBuyLeaderPCView view)
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

        private static void OnRecruitCommand(BlueprintArmyLeader blueprintArmyLeader, ArmyCartBuyLeaderView view)
        {
            var popup = new NetworkGlobalMapCommonPopup { Type = NetworkGlobalMapCommonPopupType.BuyLeader };
            var message = string.Format(UIStrings.Instance.CrusadeTexts.BuyLeaderFormat, blueprintArmyLeader.LeaderName, view.ViewModel.CostFinance.Value);
            UIUtility.ShowMessageBox(message, MessageModalBase.ModalType.Dialog, button =>
            {
                if (button != MessageModalBase.ButtonType.Yes)
                {
                    Main.Multiplayer.OnGlobalMapCommonPopupDeclined(popup);
                    return;
                }
                Main.Multiplayer.OnGlobalMapCommonPopupAccepted(popup);
                view.ViewModel.OnRecruitPublic(blueprintArmyLeader);
            }, null, 0, null, null, null);
            Main.Multiplayer.OnGlobalMapCommonPopupShown(popup);
        }
    }
}
