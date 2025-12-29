using System;
using System.Collections.Generic;
using HarmonyLib;
using Kingmaker.UI.MVVM._CommonView.CharGen.Phases.Common;
using Kingmaker.UI.MVVM._PCView.CharGen.Phases;
using Kingmaker.UI.MVVM._PCView.CharGen.Phases.AbilityScores;
using Kingmaker.UI.MVVM._PCView.CharGen.Phases.Name;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Entities.Leveling;

namespace WOTRMultiplayer.HarmonyPatches.Leveling
{
    [HarmonyPatch]
    public class SequentialSelectorCommonViewPatches
    {
        private static readonly Dictionary<Type, Action<SequentialSelectorCommonView, NetworkLevelingSequenceDirection>> _selectorHandlers = new()
        {
            { typeof(CharGenAbilityScoresDetailedPCView), (view, direction) => Main.Multiplayer.OnLevelingRacialAbilityScoreBonusChanged(direction) },
            { typeof(CharGenNamePhaseDetailedPCView), OnCharGenNameSelectorChanged }
        };

        [HarmonyPatch(typeof(SequentialSelectorCommonView), nameof(SequentialSelectorCommonView.OnNextHandler))]
        [HarmonyPrefix]
        public static bool SequentialSelectorCommonView_OnNextHandler_Prefix(SequentialSelectorCommonView __instance, ref bool __result)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var onHandler = OnHandler(__instance, NetworkLevelingSequenceDirection.Right);
            if (!onHandler)
            {
                __result = false;
            }

            return onHandler;
        }

        [HarmonyPatch(typeof(SequentialSelectorCommonView), nameof(SequentialSelectorCommonView.OnPreviousHandler))]
        [HarmonyPrefix]
        public static bool SequentialSelectorCommonView_OnPreviousHandler_Prefix(SequentialSelectorCommonView __instance, ref bool __result)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var onHandler = OnHandler(__instance, NetworkLevelingSequenceDirection.Left);
            if (!onHandler)
            {
                __result = false;
            }

            return onHandler;
        }

        private static bool OnHandler(SequentialSelectorCommonView view, NetworkLevelingSequenceDirection direction)
        {
            var currentView = GetCurrentCharGenDetailView();
            if (currentView != null && _selectorHandlers.TryGetValue(currentView.GetType(), out var handler))
            {
                var canInteract = Main.Multiplayer.CanMakeLevelingDecisions();
                if (!canInteract)
                {
                    return false;
                }

                handler(view, direction);
            }

            return true;
        }

        private static void OnCharGenNameSelectorChanged(SequentialSelectorCommonView view, NetworkLevelingSequenceDirection direction)
        {
            if (view.name.EndsWith("Month", StringComparison.OrdinalIgnoreCase))
            {
                Main.Multiplayer.OnLevelingBirthMonthChanged(direction);
                return;
            }

            Main.Multiplayer.OnLevelingBirthDayChanged(direction);
        }

        private static ICharGenPhaseDetailedView GetCurrentCharGenDetailView()
        {
            var charGenView = CharGenViewAccessor.GetCharGenContextView()?.m_CharGenPCView;
            if (charGenView == null)
            {
                Main.GetLogger<SequentialSelectorCommonViewPatches>().LogError("Unable to find char gen pc view");
                return null;
            }

            return charGenView?.SelectedDetailView;
        }
    }
}
