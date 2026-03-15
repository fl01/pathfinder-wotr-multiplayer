using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.UI;
using Kingmaker.UI.MVVM._PCView.Crusade.LeaderLevelUp;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.Controls.Other;
using UniRx;

namespace WOTRMultiplayer.HarmonyPatches.GlobalMap
{
    [HarmonyPatch]
    public class LeaderLevelUpPatches
    {
        [HarmonyPatch(typeof(LeaderLevelUpPCView), nameof(LeaderLevelUpPCView.BindViewImplementation))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> LeaderLevelUpPCView_BindViewImplementation_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(EscHotkeyManager), nameof(EscHotkeyManager.Subscribe));
            var replaceWith = AccessTools.Method(typeof(LeaderLevelUpPatches), nameof(LeaderLevelUpPatches.SubscribeEnterMessageEscPress));
            var matcher = new CodeMatcher(instructions);

            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<LeaderLevelUpPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith),
            };
            match = match.Advance(-6).RemoveInstructions(7).Insert(newInstructions);
            Main.GetLogger<LeaderLevelUpPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(LeaderLevelUpPCView), nameof(LeaderLevelUpPCView.BindViewImplementation))]
        [HarmonyPostfix]
        public static void LeaderLevelUpPCView_BindViewImplementation_Postfix(LeaderLevelUpPCView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            __instance.AddDisposable(__instance.CanConfirm.Subscribe(value => __instance.m_ConfirmButton.Interactable = value && __instance.m_CloseButton.Interactable));
            __instance.AddDisposable(__instance.m_CloseButton.OnLeftClickAsObservable().Subscribe(_ => Main.Multiplayer.OnGlobalMapCrusadeArmyLeaderLevelingClosed()));
            __instance.AddDisposable(__instance.m_ConfirmButton.OnLeftClickAsObservable().Subscribe(_ => Main.Multiplayer.OnGlobalMapCrusadeArmyLeaderLevelingConfirmed()));
            __instance.AddDisposable(__instance.ViewModel.SelectedSkill.Subscribe(skill =>
            {
                var skillId = skill?.AssetGuid.ToString();
                if (skillId == null)
                {
                    return;
                }

                Main.Multiplayer.OnGlobalMapCrusadeArmyLeaderLevelingSkillSelected(skillId);
            }));

            Main.Multiplayer.OnGlobalMapCrusadeArmyLeaderLevelingShown();
        }

        private static IDisposable SubscribeEnterMessageEscPress(LeaderLevelUpPCView view)
        {
            var disposable = Game.Instance.UI.EscManager.Subscribe(() =>
            {
                if (!Main.Multiplayer.IsActive || view.m_CloseButton.Interactable)
                {
                    if (Main.Multiplayer.IsActive)
                    {
                        Main.Multiplayer.OnGlobalMapCrusadeArmyLeaderLevelingClosed();
                    }

                    view.ViewModel.Close();
                }
            });
            return disposable;
        }
    }
}
