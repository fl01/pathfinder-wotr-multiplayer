using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Settings;
using Kingmaker.Settings.Difficulty;
using Kingmaker.UI;
using Kingmaker.UI.MVVM._PCView.NewGame;
using Kingmaker.UI.MVVM._PCView.NewGame.Difficulty;
using Kingmaker.UI.MVVM._VM.NewGame;
using Kingmaker.UI.MVVM._VM.NewGame.Difficulty;
using Kingmaker.UI.MVVM._VM.Settings.Entities;
using Kingmaker.UI.SettingsUI;
using Microsoft.Extensions.Logging;
using UniRx;
using WOTRMultiplayer.Entities.NewGame;

namespace WOTRMultiplayer.HarmonyPatches.NewGameSequence
{
    [HarmonyPatch]
    public class NewGameSequencePatches
    {
        [HarmonyPatch(typeof(NewGamePCView), nameof(NewGamePCView.BindViewImplementation))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> NewGamePCView_BindViewImplementation_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Field(typeof(UIAccess), nameof(UIAccess.EscManager));
            var replaceWith = AccessTools.Method(typeof(NewGameSequencePatches), nameof(ConfigureCloseHandlers));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.LoadsField(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<NewGameSequencePatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            match = match.Advance(-3).RemoveInstructions(10);
            var newInstructions = new List<CodeInstruction>()
            {
                new (OpCodes.Ldarg_0),
                new (OpCodes.Call, replaceWith),
            };
            match = match.Insert(newInstructions);

            Main.GetLogger<NewGameSequencePatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        public static void ConfigureCloseHandlers(NewGamePCView __instance)
        {
            __instance.AddDisposable(Game.Instance.UI.EscManager.Subscribe(() => OnCloseNewGameView(__instance)));
        }

        [HarmonyPatch(typeof(DifficultySettingsController), nameof(DifficultySettingsController.WantChangeGameDifficulty))]
        [HarmonyPrefix]
        public static bool DifficultySettingsController_WantChangeGameDifficulty_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            return false;
        }

        [HarmonyPatch(typeof(NewGameVM), nameof(NewGameVM.OnStoryMenuSelect))]
        [HarmonyPrefix]
        public static void NewGameVM_OnStoryMenuSelect_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var phase = new NetworkNewGameSequencePhase
            {
                Type = NetworkNewGameSequencePhaseType.Story,
            };
            Main.Multiplayer.OnNewGameSequenceWitnessPhase(phase);
        }

        [HarmonyPatch(typeof(NewGameVM), nameof(NewGameVM.OnDifficultyMenuSelect))]
        [HarmonyPrefix]
        public static void NewGameVM_OnDifficultyMenuSelect_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var phase = new NetworkNewGameSequencePhase
            {
                Type = NetworkNewGameSequencePhaseType.Difficulty,
            };
            Main.Multiplayer.OnNewGameSequenceWitnessPhase(phase);
        }

        [HarmonyPatch(typeof(NewGameVM), nameof(NewGameVM.OnSaveInjectionMenuSelect))]
        [HarmonyPrefix]
        public static void NewGameVM_OnSaveInjectionMenuSelect_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var phase = new NetworkNewGameSequencePhase
            {
                Type = NetworkNewGameSequencePhaseType.SaveInjector,
            };
            Main.Multiplayer.OnNewGameSequenceWitnessPhase(phase);
        }

        [HarmonyPatch(typeof(NewGamePhaseDifficultyPCView), nameof(NewGamePhaseDifficultyPCView.BindViewImplementation))]
        [HarmonyPostfix]
        public static void NewGamePhaseDifficultyPCView_BindViewImplementation_Postfix(NewGamePhaseDifficultyPCView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var canChangeSettings = Main.Multiplayer.CanMakeNewGameSequenceDecisions();

            foreach (var setting in __instance.ViewModel.SettingEntities)
            {
                if (setting is SettingsEntityWithValueVM valueSetting)
                {
                    if (valueSetting.m_UISettingsEntity.SettingsEntity == SettingsRoot.Difficulty.GameDifficulty)
                    {
                        var difficultySetting = (UISettingsEntityGameDifficulty)valueSetting.m_UISettingsEntity;
                        __instance.AddDisposable(difficultySetting.OnValueChange.Subscribe(difficulty =>
                        {
                            var rawDifficulty = difficulty.ToString();
                            Main.Multiplayer.OnNewGameDifficultyChanged(rawDifficulty);
                        }));

                        SetSettingState(valueSetting, canChangeSettings);
                        continue;
                    }

                    SetSettingState(valueSetting, false);
                }
            }
        }

        [HarmonyPatch(typeof(NewGamePhaseDifficultyVM), nameof(NewGamePhaseDifficultyVM.OnBeginView))]
        [HarmonyPostfix]
        public static void NewGamePhaseDifficultyVM_OnBeginView_Postfix(NewGamePhaseDifficultyVM __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            __instance.SettingOnlyOneSaveVM.ModificationAllowed.Value = false;
            __instance.SettingOnlyActiveCompanionsReceiveExperienceVM.ModificationAllowed.Value = false;
        }

        private static void SetSettingState(SettingsEntityWithValueVM valueSettingVM, bool isEnabled)
        {
            var field = GetField(valueSettingVM.m_UISettingsEntity);
            field.SetValue(valueSettingVM.m_UISettingsEntity, !isEnabled);
            valueSettingVM.ModificationAllowed.Value = isEnabled;
        }

        private static FieldInfo GetField(IUISettingsEntityWithValueBase settingEntity)
        {
            var field = settingEntity.GetType().GetField(nameof(UISettingsEntityWithValueBase<object>.ManualModificationLock));
            return field;
        }

        private static void OnCloseNewGameView(NewGamePCView __instance)
        {
            if (!Main.Multiplayer.IsActive || __instance.m_CloseButton.Interactable)
            {
                __instance.ViewModel.OnClose();
            }
        }
    }
}
