using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Kingmaker.UI.SettingsUI;

namespace WOTRMultiplayer.HarmonyPatches.MenuPatches
{
    [HarmonyPatch]
    public class UISettingsManagerPatches
    {
        private static readonly HashSet<string> UnmodifiableValues = new(
            [
                "GameMainSettingsGroup.AcceleratedMove",
                "GameMainSettingsGroup.AllowLootingInCombat",
                "TurnBased.EnableTurnBased",
                "TurnBased.AutoEndTurn",
                "TurnBased.AutoStopAfterFirstMoveAction",
                "TurnBased.TimeScaleInPlayerTurn",
                "TurnBased.TimeScaleInNonPlayerTurn",
            ],
            StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<UISettingsEntityBase> _disabledSettings = [];

        [HarmonyPatch(typeof(UISettingsManager), nameof(UISettingsManager.Initialize))]
        [HarmonyPostfix]
        public static void UISettingsManager_Initialize_Postfix(UISettingsManager __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                foreach (var setting in _disabledSettings)
                {
                    EnableSetting(setting);
                }

                return;
            }

            UpdateGameSettings(__instance);
            UpdateDifficultySettings(__instance);
        }

        private static void UpdateDifficultySettings(UISettingsManager uiSettingsManager)
        {
            foreach (var settingsGroup in uiSettingsManager.m_DifficultySettingsList)
            {
                foreach (var settingEntity in settingsGroup.SettingsList)
                {
                    DisableSetting(settingEntity);
                }
            }
        }

        private static void UpdateGameSettings(UISettingsManager uiSettingsManager)
        {
            const string GameAutopauseSettingsGroup = "GameAutopauseSettingsGroup";

            foreach (var settingsGroup in uiSettingsManager.m_GameSettingsList)
            {
                var settingsGroupName = settingsGroup.name;
                foreach (var settingEntity in settingsGroup.SettingsList)
                {
                    var settingName = settingEntity.name;
                    var fullSettingName = $"{settingsGroupName}.{settingName}";
                    if (settingsGroupName != GameAutopauseSettingsGroup && !UnmodifiableValues.Contains(fullSettingName))
                    {
                        continue;
                    }

                    DisableSetting(settingEntity);
                }
            }
        }

        private static void DisableSetting(UISettingsEntityBase settingEntity)
        {
            var field = GetField(settingEntity);
            if (!(bool)field.GetValue(settingEntity))
            {
                // UISettingsEntityBase instances are alive while game is opened, so we actually need to remove modification lock if player is in the mainmenu/single player game
                _disabledSettings.Add(settingEntity);
                field?.SetValue(settingEntity, true);
            }
        }

        private static FieldInfo GetField(UISettingsEntityBase settingEntity)
        {
            var field = settingEntity.GetType().GetField(nameof(UISettingsEntityWithValueBase<object>.ManualModificationLock));
            return field;
        }

        private static void EnableSetting(UISettingsEntityBase settingEntity)
        {
            var field = GetField(settingEntity);
            field?.SetValue(settingEntity, false);
        }
    }
}