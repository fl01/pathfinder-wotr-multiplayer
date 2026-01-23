using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.Armies.State;
using Kingmaker.Kingdom.Armies;
using Kingmaker.UI.MVVM._PCView.Crusade.ArmyInfo;
using Kingmaker.UI.MVVM._VM.Crusade.ArmyInfo;
using Microsoft.Extensions.Logging;
using UnityEngine;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.GlobalMap;

namespace WOTRMultiplayer.HarmonyPatches.GlobalMap
{
    [HarmonyPatch]
    public class ArmyDataPatches
    {
        [HarmonyPatch(typeof(ArmySquadsVM), nameof(ArmySquadsVM.SplitRequest), [typeof(ArmyInfoSquadVM), typeof(ArmyInfoSquadVM)])]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> ArmySquadsVM_SplitRequest_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var extraCall = AccessTools.Method(typeof(ArmyDataPatches), nameof(ArmyDataPatches.OnArmySquadSplitRequest));
            var lookFor = AccessTools.Field(typeof(ArmySquadsVM), nameof(ArmySquadsVM.SplitVM));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.LoadsField(lookFor));

            if (match.IsInvalid)
            {
                Main.GetLogger<ArmyDataPatches>().LogError("Invalid transpiler position. Target={Target}, Position={Position}", target, match.Pos);
                return instructions;
            }

            var newInstruction = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_1),
                new(OpCodes.Ldarg_2),
                new(OpCodes.Call, extraCall),
            };
            match = match.RemoveInstructions(8).Insert(newInstruction);
            Main.GetLogger<ArmyDataPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(ArmySquadsVM), nameof(ArmySquadsVM.SwitchSquads))]
        [HarmonyPrefix]
        public static void ArmySquadsVM_SwitchSquads_Prefix(ArmyInfoSquadVM sourceVm, ArmyInfoSquadVM targetVm)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var sourceSquadSlot = CreateSquadSlot(sourceVm);
            var targetSquadSlot = CreateSquadSlot(targetVm);
            Main.Multiplayer.OnGlobalMapCrusadeArmySquadsSwitched(sourceSquadSlot, targetSquadSlot);
        }

        [HarmonyPatch(typeof(ArmySquadsVM), nameof(ArmySquadsVM.MergeSquads))]
        [HarmonyPrefix]
        public static void ArmySquadsVM_MergeSquads_Prefix(ArmyInfoSquadVM sourceVm, ArmyInfoSquadVM targetVm, int count)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var sourceSquadSlot = CreateSquadSlot(sourceVm);
            var targetSquadSlot = CreateSquadSlot(targetVm);
            Main.Multiplayer.OnGlobalMapCrusadeArmySquadsMerged(sourceSquadSlot, targetSquadSlot, count);
        }

        [HarmonyPatch(typeof(ArmyInfoSquadVM), nameof(ArmyInfoSquadVM.MergeInOne))]
        [HarmonyPrefix]
        public static bool ArmyInfoSquadVM_MergeInOne_Prefix(ArmyInfoSquadVM __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var sourceSquadSlot = CreateSquadSlot(__instance);
            var canContinue = Main.Multiplayer.OnGlobalMapCrusadeArmyMergedInOne(sourceSquadSlot);
            return canContinue;
        }

        [HarmonyPatch(typeof(ArmyInfoSquadVM), nameof(ArmyInfoSquadVM.SplitCount))]
        [HarmonyPrefix]
        public static bool ArmyInfoSquadVM_SplitCount_Prefix(ArmyInfoSquadVM __instance, int count)
        {
            if (!Main.Multiplayer.IsActive || __instance.Count.Value <= 1)
            {
                return true;
            }

            var sourceSquadSlot = CreateSquadSlot(__instance);
            var canContinue = Main.Multiplayer.OnGlobalMapCrusadeArmySquadSplitted(sourceSquadSlot, count);
            return canContinue;
        }

        [HarmonyPatch(typeof(ArmyDismissManager), nameof(ArmyDismissManager.DismissSquad))]
        [HarmonyPrefix]
        public static void ArmyDismissManager_DismissSquad_Prefix(ArmyData army, SquadState squad)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var position = army.GetSquadPosition(squad);
            if (!position.HasValue)
            {
                // should never happen?
                Main.GetLogger<ArmyDataPatches>().LogError("ArmyDismissManager_Dismiss_Prefix - Squad position is null");
                return;
            }
            var squadSlot = CreateSquadSlot(army, squad, position.Value);
            Main.Multiplayer.OnGlobalMapCrusadeArmySquadDismiss(squadSlot);
        }

        [HarmonyPatch(typeof(ArmyInfoSquadPCView), nameof(ArmyInfoSquadPCView.BindViewImplementation))]
        [HarmonyPostfix]
        public static void ArmyInfoSquadPCView_BindViewImplementation_Postfix(ArmyInfoSquadPCView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            __instance.m_DismissButton.Interactable = Main.Multiplayer.CanNavigateOnGlobalMap();
        }

        [HarmonyPatch(typeof(ArmyInfoSquadPCView), nameof(ArmyInfoSquadPCView.OnBeginDrag))]
        [HarmonyPrefix]
        public static bool ArmyInfoSquadPCView_OnBeginDrag_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.CanNavigateOnGlobalMap();
            return canContinue;
        }

        [HarmonyPatch(typeof(ArmyInfoSquadPCView), nameof(ArmyInfoSquadPCView.OnEndDrag))]
        [HarmonyPrefix]
        public static bool ArmyInfoSquadPCView_OnEndDrag_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.CanNavigateOnGlobalMap();
            return canContinue;
        }

        [HarmonyPatch(typeof(ArmyInfoSquadPCView), nameof(ArmyInfoSquadPCView.OnDrag))]
        [HarmonyPrefix]
        public static bool ArmyInfoSquadPCView_OnDrag_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.CanNavigateOnGlobalMap();
            return canContinue;
        }

        private static void OnArmySquadSplitRequest(ArmySquadsVM armySquadsVM, ArmyInfoSquadVM sourceVm, ArmyInfoSquadVM targetVm)
        {
            armySquadsVM.SplitVM.Value = new ArmySquadsSplitVM(sourceVm, count =>
            {
                if (count > 0)
                {
                    var errors = armySquadsVM.m_State.Data.MergeSquads(sourceVm.SquadPosition, targetVm.SquadPosition, count);
                    if (Main.Multiplayer.IsActive && errors == SquadErrors.None)
                    {
                        var sourceSquadSlot = CreateSquadSlot(sourceVm);
                        var targetSquadSlot = CreateSquadSlot(targetVm);
                        Main.Multiplayer.OnGlobalMapCrusadeArmySquadSplitRequested(sourceSquadSlot, targetSquadSlot, count);
                    }

                    armySquadsVM.SquadErrorsTrigger.Execute(errors);
                }
                ArmySquadsSplitVM value = armySquadsVM.SplitVM.Value;
                value?.Dispose();
                armySquadsVM.SplitVM.Value = null;
            });
        }

        private static NetworkGlobalMapArmySquadSlot CreateSquadSlot(ArmyInfoSquadVM armyInfoSquadVM)
        {
            return CreateSquadSlot(armyInfoSquadVM.Army, armyInfoSquadVM.Squad, armyInfoSquadVM.SquadPosition);
        }

        private static NetworkGlobalMapArmySquadSlot CreateSquadSlot(ArmyData army, SquadState squad, Vector2Int position)
        {
            var squadSlot = new NetworkGlobalMapArmySquadSlot
            {
                ArmyId = army.ArmyStateId,
                SquadId = squad?.Id,
                Position = new NetworkVector2Int(position.x, position.y)
            };

            return squadSlot;
        }
    }
}
