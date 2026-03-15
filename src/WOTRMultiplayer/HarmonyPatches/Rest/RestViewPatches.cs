using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.UI;
using Kingmaker.UI.MVVM;
using Kingmaker.UI.MVVM._PCView.Rest;
using Kingmaker.UI.MVVM._VM.Rest;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches.Rest
{
    [HarmonyPatch]
    public class RestViewPatches
    {
        [HarmonyPatch(typeof(RestPCView), nameof(RestPCView.BindViewImplementation))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RestPCView_BindViewImplementation_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(EscHotkeyManager), nameof(EscHotkeyManager.Subscribe));
            var replaceWith = AccessTools.Method(typeof(RestViewPatches), nameof(RestViewPatches.SubscribeEnterMessageEscPress));
            var matcher = new CodeMatcher(instructions);

            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<RestViewPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, replaceWith),
            };
            match = match.Advance(-6).RemoveInstructions(7).Insert(newInstructions);
            Main.GetLogger<RestViewPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        /// <summary>
        /// 'Use Spells' toggle is a field sadly, so this patch is required
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPatch(typeof(RestPCView), nameof(RestPCView.SetHealingState))]
        [HarmonyPrefix]
        public static bool RestPCView_SetHealingState_Prefix(RestPCView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var shouldContinue = Main.Multiplayer.OnCampingUseHealingSpellsChanged(__instance.m_HealingToggle.isOn);
            return shouldContinue;
        }

        [HarmonyPatch(typeof(RestPCView), nameof(RestPCView.StartRest))]
        [HarmonyPrefix]
        public static void RestPCView_StartRest_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnStartRest();
        }

        [HarmonyPatch(typeof(RestBaseView), nameof(RestBaseView.CloseRest))]
        [HarmonyPrefix]
        public static void RestBaseView_RestBaseView_Prefix(RestBaseView __instance)
        {
            if (!Main.Multiplayer.IsActive || __instance.ViewModel.CurrentPhase.Value == UIRestPhase.InProcess)
            {
                return;
            }

            var shouldNotifyAboutClose = __instance.ViewModel.CurrentPhase.Value == UIRestPhase.Management
                && (RootUIContext.Instance.IsGlobalMap || Game.Instance.Player.CapitalPartyMode);

            if (shouldNotifyAboutClose)
            {
                Main.Multiplayer.OnRestWindowClosed();
            }
        }

        private static IDisposable SubscribeEnterMessageEscPress(RestPCView view)
        {
            var disposable = Game.Instance.UI.EscManager.Subscribe(() =>
            {
                if (!Main.Multiplayer.IsActive || view.m_CloseButton.interactable)
                {
                    view.CloseRest();
                }
            });

            return disposable;
        }
    }
}
