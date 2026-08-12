using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.Settings;
using Kingmaker.UI;
using Kingmaker.UI.MVVM._VM.Settings;
using Kingmaker.UI.MVVM._VM.Settings.Entities;
using Kingmaker.UI.SettingsUI;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.UI;
using WOTRMultiplayer.UI.Settings;

namespace WOTRMultiplayer.HarmonyPatches.MenuPatches
{
    [HarmonyPatch]
    public class SettingsVMPatches
    {
        [HarmonyPatch(typeof(SettingsVM), MethodType.Constructor, [typeof(Action), typeof(bool), typeof(UISettingsManager.SettingsScreen)])]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> SettingsVM_Constructor_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var matcher = new CodeMatcher(instructions);
            var lookFor = AccessTools.Method(typeof(SettingsVM), nameof(SettingsVM.CreateMenuEntity));
            var createMenuCall = AccessTools.Method(typeof(SettingsVMPatches), nameof(SettingsVMPatches.CreateMultiplayerSettingsMenu));
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<SettingsVMPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return matcher.Instructions();
            }

            match = match.Advance(1);
            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, createMenuCall)
            };
            match.Insert(newInstructions);
            Main.GetLogger<SettingsVMPatches>().LogDebug("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(SettingsVM), nameof(SettingsVM.SetSettingsList))]
        [HarmonyPostfix]
        public static void SettingsVM_SetSettingsList_Postfix(SettingsVM __instance, UISettingsManager.SettingsScreen settingsScreen)
        {
            if (settingsScreen == UIFactory.MultiplayerSettingsMenuId)
            {
                __instance.IsDefaultButtonInteractable.Value = !__instance.IsUIReloadDenied();
            }
        }

        [HarmonyPatch(typeof(SettingsVM), nameof(SettingsVM.OnDefaultDialogAnswer))]
        [HarmonyPrefix]
        public static bool SettingsVM_ResetToDefault_Prefix(SettingsVM __instance, MessageModalBase.ButtonType buttonType)
        {
            if (__instance.m_SelectedMenuEntity.Value.SettingsScreenType != UIFactory.MultiplayerSettingsMenuId || buttonType != MessageModalBase.ButtonType.Yes)
            {
                return true;
            }

            SettingsController.RevertAllTempValues();
            foreach (var setting in __instance.m_SettingEntities)
            {
                if (setting is ISettingsEntityInputVM inputViewModel) // custom controls (input for now)
                {
                    inputViewModel.RevertToDefault();
                }
                else if (setting is SettingsEntityWithValueVM entityWithValue) // built-in controls
                {
                    entityWithValue.ResetToDefault();
                }
            }

            __instance.HandleItemChanged();
            Main.GetLogger<SettingsVMPatches>().LogInformation("Multiplayer settings have been reverted to default value");
            return false;
        }

        [HarmonyPatch(typeof(SettingsVM), nameof(SettingsVM.RevertSettings))]
        [HarmonyPostfix]
        public static void SettingsVM_RevertSettings_Postfix(SettingsVM __instance)
        {
            if (__instance.m_SelectedMenuEntity.Value.SettingsScreenType != UIFactory.MultiplayerSettingsMenuId)
            {
                return;
            }

            foreach (var setting in __instance.m_SettingEntities)
            {
                if (setting is ISettingsEntityInputVM inputViewModel)
                {
                    inputViewModel.RevertTempValue();
                }
            }

            Main.GetLogger<SettingsVMPatches>().LogInformation("Multiplayer settings have been reverted");
        }

        [HarmonyPatch(typeof(SettingsVM), nameof(SettingsVM.ApplySettings))]
        [HarmonyPrefix]
        public static void SettingsVM_ApplySettings_Prefix(SettingsVM __instance)
        {
            if (__instance.m_SelectedMenuEntity.Value.SettingsScreenType != UIFactory.MultiplayerSettingsMenuId)
            {
                return;
            }

            foreach (var setting in __instance.m_SettingEntities)
            {
                if (setting is ISettingsEntityInputVM inputViewModel && !inputViewModel.IsValid)
                {
                    inputViewModel.RevertTempValue();
                }
            }

            Main.GetLogger<SettingsVMPatches>().LogInformation("Multiplayer settings have been applied");
        }

        [HarmonyPatch(typeof(SettingsVM), nameof(SettingsVM.SwitchSettingsScreen))]
        [HarmonyPrefix]
        public static bool SettingsVM_SwitchSettingsScreen_Prefix(SettingsVM __instance, UISettingsManager.SettingsScreen settingsScreen)
        {
            if (settingsScreen != UIFactory.MultiplayerSettingsMenuId)
            {
                return true;
            }

            Main.Multiplayer.UIFactory.PopulateMultiplayerSettingsUI(__instance, !Main.Multiplayer.IsActive);

            return false;
        }

        public static void CreateMultiplayerSettingsMenu(SettingsVM settingsVM)
        {
            Main.Multiplayer.UIFactory.CreateMultiplayerSettingsMenu(settingsVM);
        }
    }
}
