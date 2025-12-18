using System;
using System.Collections.Generic;
using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.CharGen.Phases.Mythic;
using Kingmaker.UI.MVVM._PCView.CharGen.Phases.Portrait;
using Kingmaker.UI.MVVM._PCView.CharGen.Phases.Race;
using Kingmaker.UI.MVVM._VM.CharGen.Phases.Mythic;
using Kingmaker.UI.MVVM._VM.CharGen.Phases.Portrait;
using Kingmaker.UI.MVVM._VM.CharGen.Phases.Race;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.SelectionGroup;
using Owlcat.Runtime.UI.SelectionGroup.View;
using UniRx;
using WOTRMultiplayer.MP.Entities.Leveling;

namespace WOTRMultiplayer.HarmonyPatches.Leveling
{
    [HarmonyPatch]
    public class CharGenSelectionGroupEntityPatches
    {
        private static readonly Dictionary<Type, Func<SelectionGroupEntityView<SelectionGroupEntityVM>, IDisposable>> _phases = new()
        {
            { typeof(CharGenMythicSelectorItemPCView),  x => LevelingMythicClassSelected((CharGenMythicSelectorItemVM)x.ViewModel) },
            { typeof(CharGenPortraitSelectorItemPCView),  x => LevelingPortraitSelected((CharGenPortraitSelectorItemVM)x.ViewModel) },
            { typeof(CharGenRaceSelectorItemPCView),  x => LevelingRaceSelected((CharGenRaceSelectorItemVM)x.ViewModel) },
        };

        [HarmonyPatch(typeof(SelectionGroupEntityView<CharGenMythicSelectorItemVM>), nameof(SelectionGroupEntityView<CharGenMythicSelectorItemVM>.OnClick))]
        [HarmonyPrefix]
        public static bool SelectionGroupEntityView_OnClick_Prefix(SelectionGroupEntityView<SelectionGroupEntityVM> __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var type = __instance.GetType();
            return !_phases.ContainsKey(type) || Main.Multiplayer.CanMakeLevelingDecisions();
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
                handler(__instance);
            }
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
                Main.GetLogger<CharGenSelectionGroupEntityPatches>().LogWarning("Selected race. Id={Id}", raceId);
            });
        }
    }
}
