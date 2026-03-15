using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.UI;
using Kingmaker.UI.MVVM._PCView.Crusade.ArmyInfo;
using Kingmaker.UI.MVVM._VM.Crusade.ArmyInfo;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches.GlobalMap
{
    [HarmonyPatch]
    public class ArmyInfoViewPatches
    {
        [HarmonyPatch(typeof(ArmyInfoPCView), nameof(ArmyInfoPCView.BindViewImplementation))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> ArmyInfoPCView_BindViewImplementation_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(EscHotkeyManager), nameof(EscHotkeyManager.Subscribe));
            var replaceWith = AccessTools.Method(typeof(ArmyInfoViewPatches), nameof(ArmyInfoViewPatches.SubscribeEnterMessageEscPress));
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
            match = match.Advance(-6).RemoveInstructions(7).Insert(newInstructions);
            Main.GetLogger<ArmyInfoViewPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(ArmyInfoView), nameof(ArmyInfoView.CreateArmy))]
        [HarmonyPrefix]
        public static void ArmyInfoView_CreateArmy_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnGlobalMapCreateCrusadeArmy();
        }

        [HarmonyPatch(typeof(ArmyInfoPCView), nameof(ArmyInfoPCView.BindViewImplementation))]
        [HarmonyPostfix]
        public static void ArmyInfoPCView_BindViewImplementation_Postfix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnGlobalMapCrusadeArmyInfoShown();
        }

        [HarmonyPatch(typeof(ArmyInfoHUDPCView), nameof(ArmyInfoHUDPCView.BindViewImplementation))]
        [HarmonyPostfix]
        public static void ArmyInfoHUDPCView_BindViewImplementation_Postfix(ArmyInfoHUDPCView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            __instance.m_InfoButton.Interactable = Main.Multiplayer.CanControlGlobalMap();
        }

        [HarmonyPatch(typeof(ArmyInfoVM), nameof(ArmyInfoVM.NextMergeArmy))]
        [HarmonyPrefix]
        public static void ArmyInfoVM_NextMergeArmy_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnGlobalMapCrusadeArmyInfoNextMergeArmy();
        }

        [HarmonyPatch(typeof(ArmyInfoVM), nameof(ArmyInfoVM.PrevMergeArmy))]
        [HarmonyPrefix]
        public static void ArmyInfoVM_PrevMergeArmy_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnGlobalMapCrusadeArmyInfoPrevMergeArmy();
        }

        [HarmonyPatch(typeof(ArmyInfoVM), nameof(ArmyInfoVM.MoveSquadsToMainArmy))]
        [HarmonyPrefix]
        public static void ArmyInfoVM_MoveSquadsToMainArmy_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnGlobalMapCrusadeArmyMoveSquadsToMainArmy();
        }

        [HarmonyPatch(typeof(ArmyInfoVM), nameof(ArmyInfoVM.MoveSquadsToSecondArmy))]
        [HarmonyPrefix]
        public static void ArmyInfoVM_MoveSquadsToSecondArmy_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnGlobalMapCrusadeArmyMoveSquadsToSecondArmy();
        }

        private static IDisposable SubscribeEnterMessageEscPress(ArmyInfoPCView view)
        {
            return Game.Instance.UI.EscManager.Subscribe(() =>
            {
                if (!Main.Multiplayer.IsActive || ((ArmyInfoArmyCartPCView)view.m_MainArmyCartView).m_CloseButton.Interactable)
                {
                    if (Main.Multiplayer.IsActive)
                    {
                        Main.Multiplayer.OnGlobalMapCrusadeArmyInfoClosed();
                    }

                    view.ViewModel.OnClose();
                }
            });
        }
    }
}
