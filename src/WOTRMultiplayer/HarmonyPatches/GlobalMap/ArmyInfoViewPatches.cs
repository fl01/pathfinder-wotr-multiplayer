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
using WOTRMultiplayer.Entities.GlobalMap;

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
            match = match.RemoveInstructions(1)
                .Insert(newInstructions);
            Main.GetLogger<ArmyInfoViewPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(ArmyInfoView), nameof(ArmyInfoView.CreateArmy))]
        [HarmonyPrefix]
        public static void ArmyInfoPCView_CreateArmy_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnGlobalMapCrusadeArmyInfoCreateArmy();
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

            __instance.m_InfoButton.Interactable = Main.Multiplayer.CanNavigateOnGlobalMap();
        }

        [HarmonyPatch(typeof(ArmyInfoArmyCartView), nameof(ArmyInfoArmyCartView.SetArmyName))]
        [HarmonyPrefix]
        public static void ArmyInfoArmyCartView_SetArmyName_Prefix(ArmyInfoArmyCartView __instance, string armyName)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var globalMapArmy = new NetworkGlobalMapArmy { Id = __instance.ViewModel.State?.Id, Name = armyName };
            Main.Multiplayer.OnGlobalMapCrusadeArmyInfoCartNameChanged(globalMapArmy);
        }

        [HarmonyPatch(typeof(ArmyInfoArmyCartPCView), nameof(ArmyInfoArmyCartPCView.BindViewImplementation))]
        [HarmonyPostfix]
        public static void ArmyInfoArmyCartPCView_BindViewImplementation_Postfix(ArmyInfoArmyCartPCView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var armyInfo = Main.UIAccessor.GlobalMapPCView?.m_ArmyInfoPCView;
            if (armyInfo?.m_MergeArmyCartView == __instance)
            {
                Main.Multiplayer.OnGlobalMapCrusadeArmyInfoMergeShown();
            }
        }

        [HarmonyPatch(typeof(ArmyInfoArmyCartVM), nameof(ArmyInfoArmyCartVM.OnClose))]
        [HarmonyPrefix]
        public static void ArmyInfoArmyCartVM_OnClose_Prefix(ArmyInfoArmyCartVM __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var armyInfo = Main.UIAccessor.GlobalMapPCView?.m_ArmyInfoPCView;
            if (armyInfo?.m_MainArmyCartView?.ViewModel == __instance)
            {
                Main.Multiplayer.OnGlobalMapCrusadeArmyInfoMainClosed();
            }
            else if (armyInfo?.m_MergeArmyCartView?.ViewModel == __instance)
            {
                Main.Multiplayer.OnGlobalMapCrusadeArmyInfoMergeClosed();
            }
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

        private IDisposable SubscribeEnterMessageEscPress(Action action, ArmyInfoPCView view)
        {
            return Game.Instance.UI.EscManager.Subscribe(() =>
            {
                if (!Main.Multiplayer.IsActive)
                {
                    action?.Invoke();
                    return;
                }

                if (((ArmyInfoArmyCartPCView)view.m_MainArmyCartView).m_CloseButton.Interactable)
                {
                    Main.Multiplayer.OnGlobalMapCrusadeArmyInfoClosed();
                    action?.Invoke();
                }
            });
        }
    }
}
