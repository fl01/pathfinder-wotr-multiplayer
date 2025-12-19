using System;
using System.Collections.Generic;
using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.CharGen.Phases.Alignment;
using Kingmaker.UI.MVVM._PCView.CharGen.Phases.Mythic;
using Kingmaker.UI.MVVM._PCView.CharGen.Phases.Portrait;
using Kingmaker.UI.MVVM._PCView.CharGen.Phases.Race;
using Kingmaker.UI.MVVM._PCView.CharGen.Phases.Voice;
using Kingmaker.UI.MVVM._VM.CharGen.Phases.Alignment;
using Kingmaker.UI.MVVM._VM.CharGen.Phases.Mythic;
using Kingmaker.UI.MVVM._VM.CharGen.Phases.Portrait;
using Kingmaker.UI.MVVM._VM.CharGen.Phases.Race;
using Kingmaker.UI.MVVM._VM.CharGen.Phases.Voice;
using Owlcat.Runtime.UI.SelectionGroup;
using Owlcat.Runtime.UI.SelectionGroup.View;
using UniRx;
using WOTRMultiplayer.MP.Entities.Leveling;

namespace WOTRMultiplayer.HarmonyPatches.Leveling
{
    [HarmonyPatch]
    public class SelectionGroupEntityViewPatches
    {
        private static readonly Dictionary<Type, Func<SelectionGroupEntityView<SelectionGroupEntityVM>, IDisposable>> _phases = new()
        {
            { typeof(CharGenMythicSelectorItemPCView),  x => LevelingMythicClassSelected((CharGenMythicSelectorItemVM)x.ViewModel) },
            { typeof(CharGenPortraitSelectorItemPCView),  x => LevelingPortraitSelected((CharGenPortraitSelectorItemVM)x.ViewModel) },
            { typeof(CharGenRaceSelectorItemPCView),  x => LevelingRaceSelected((CharGenRaceSelectorItemVM)x.ViewModel) },
            { typeof(CharGenGenderSelectorItemPCView),  x => LevelingGenderSelected((CharGenGenderItemVM)x.ViewModel) },
            { typeof(CharGenAlignmentSectorPCView),  x => LevelingAlignmentSelected((CharGenAlignmentSectorVM)x.ViewModel) },
            { typeof(CharGenVoiceItemPCView),  x => LevelingVoiceSelected((CharGenVoiceItemVM)x.ViewModel) },
        };

        [HarmonyPatch(typeof(SelectionGroupEntityView<SelectionGroupEntityVM>), nameof(SelectionGroupEntityView<SelectionGroupEntityVM>.OnClick))]
        [HarmonyPrefix]
        public static bool SelectionGroupEntityView_OnClick_Prefix(SelectionGroupEntityView<SelectionGroupEntityVM> __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var type = __instance.GetType();
            var canContinue = !_phases.ContainsKey(type) || Main.Multiplayer.CanMakeLevelingDecisions();
            return canContinue;
        }

        [HarmonyPatch(typeof(CharGenRaceSelectorItemPCView), nameof(CharGenRaceSelectorItemPCView.OnClick))]
        [HarmonyPrefix]
        public static bool CharGenRaceSelectorItemPCView_OnClick_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.CanMakeLevelingDecisions();
            return canContinue;
        }

        [HarmonyPatch(typeof(CharGenVoiceItemPCView), nameof(CharGenVoiceItemPCView.OnClick))]
        [HarmonyPrefix]
        public static bool CharGenVoiceItemPCView_OnClick_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.CanMakeLevelingDecisions();
            return canContinue;
        }

        [HarmonyPatch(typeof(SelectionGroupEntityView<SelectionGroupEntityVM>), nameof(SelectionGroupEntityView<SelectionGroupEntityVM>.BindViewImplementation))]
        [HarmonyPostfix]
        public static void SelectionGroupEntityView_BindViewImplementation_Postfix(SelectionGroupEntityView<SelectionGroupEntityVM> __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var type = __instance.GetType();
            if (_phases.TryGetValue(type, out var handler))
            {
                __instance.AddDisposable(handler(__instance));
            }
        }

        private static IDisposable LevelingVoiceSelected(CharGenVoiceItemVM viewModel)
        {
            return viewModel.IsSelected.Subscribe<bool>(isSelected =>
            {
                if (!isSelected)
                {
                    return;
                }

                var levelingVoice = new NetworkLevelingVoice
                {
                    Id = viewModel.Voice.AssetGuid.ToString(),
                    GenderId = viewModel.Gender.ToString()
                };

                Main.Multiplayer.OnLevelingVoiceSelected(levelingVoice);
            });
        }

        private static IDisposable LevelingMythicClassSelected(CharGenMythicSelectorItemVM viewModel)
        {
            return viewModel.IsSelected.Subscribe<bool>(isSelected =>
            {
                if (!isSelected)
                {
                    return;
                }

                var classId = viewModel.Class.AssetGuid.ToString();
                Main.Multiplayer.OnLevelingMythicClassSelected(classId);
            });
        }

        private static IDisposable LevelingPortraitSelected(CharGenPortraitSelectorItemVM viewModel)
        {
            return viewModel.IsSelected.Subscribe<bool>(isSelected =>
            {
                if (!isSelected)
                {
                    return;
                }

                var portrait = new NetworkLevelingPortrait
                {
                    Category = viewModel.PortraitData.PortraitCategory.ToString(),
                    CustomId = viewModel.PortraitData.CustomId,
                    Name = viewModel.PortraitData.SmallPortrait?.name
                };

                Main.Multiplayer.OnLevelingPortraitSelected(portrait);
            });
        }

        private static IDisposable LevelingRaceSelected(CharGenRaceSelectorItemVM viewModel)
        {
            return viewModel.IsSelected.Subscribe<bool>(isSelected =>
            {
                if (!isSelected)
                {
                    return;
                }

                var raceId = viewModel.Race.AssetGuid.ToString();
                Main.Multiplayer.OnLevelingRaceSelected(raceId);
            });
        }

        private static IDisposable LevelingGenderSelected(CharGenGenderItemVM viewModel)
        {
            return viewModel.IsSelected.Subscribe<bool>(isSelected =>
            {
                if (!isSelected)
                {
                    return;
                }

                var genderId = viewModel.Gender.ToString();
                Main.Multiplayer.OnLevelingGenderSelected(genderId);
            });
        }

        private static IDisposable LevelingAlignmentSelected(CharGenAlignmentSectorVM viewModel)
        {
            return viewModel.IsSelected.Subscribe<bool>(isSelected =>
            {
                if (!isSelected)
                {
                    return;
                }

                var alignmentId = viewModel.Alignment.ToString();
                Main.Multiplayer.OnLevelingAlignmentSelected(alignmentId);
            });
        }
    }
}
