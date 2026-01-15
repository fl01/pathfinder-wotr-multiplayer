using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.Globalmap;
using Kingmaker.Globalmap.View;
using Kingmaker.UI.MVVM._PCView.GlobalMap.Toolbar;
using Kingmaker.UI.MVVM._VM.Crusade.Armies;
using Kingmaker.UI.MVVM._VM.GlobalMap;
using Kingmaker.UI.MVVM._VM.GlobalMap.Toolbar;
using Microsoft.Extensions.Logging;
using UniRx;
using WOTRMultiplayer.Entities.GlobalMap;

namespace WOTRMultiplayer.HarmonyPatches.GlobalMap
{
    [HarmonyPatch]
    public class GlobalMapControlPatches
    {
        [HarmonyPatch(typeof(GlobalMapSelectController), nameof(GlobalMapSelectController.HandleClick), [typeof(GlobalMapPawn)])]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> GlobalMapSelectController_HandleClick_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(GlobalMapController), nameof(GlobalMapController.SetSelectedArmy));
            var armyClickCall = AccessTools.Method(typeof(GlobalMapControlPatches), nameof(GlobalMapControlPatches.OnArmyPawnClicked));
            var playerClickCall = AccessTools.Method(typeof(GlobalMapControlPatches), nameof(GlobalMapControlPatches.OnPlayerPawnClicked));
            var matcher = new CodeMatcher(instructions);

            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<GlobalMapControlPatches>().LogError("Transpiler has not been applied (GlobalMapArmyPawnClick). Target={Target}", target);
                return instructions;
            }

            var armyPawnInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldloc_S, 4),
                new(OpCodes.Call, armyClickCall)
            };
            match = match.Advance(-1).Insert(armyPawnInstructions);

            match = matcher.End().SearchBackwards(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<GlobalMapControlPatches>().LogError("Transpiler has not been applied (GlobalMapPlayerPawnClick). Target={Target}", target);
                return instructions;
            }
            var playerPawnInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Call, playerClickCall)
            };
            match = match.Insert(playerPawnInstructions);

            Main.GetLogger<GlobalMapControlPatches>().LogInformation("Transpiler has been applied (GlobalMapArmyPawnClick + GlobalMapPlayerPawnClick). Target={Target}", target);
            return matcher.Instructions();
        }

        [HarmonyPatch(typeof(GlobalMapToolbarPCView), nameof(GlobalMapToolbarPCView.BindViewImplementation))]
        [HarmonyPostfix]
        public static void GlobalMapToolbarPCView_BindViewImplementation_Postfix(GlobalMapToolbarPCView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            __instance.AddDisposable(__instance.ViewModel.ArmyMode.Subscribe<bool>(value =>
            {
                var travelerMode = GetTravelerMode(value);
                Main.Multiplayer.OnGlobalMapTravelerModeChanged(travelerMode);
            }));
        }

        [HarmonyPatch(typeof(GlobalMapToolbarView<GlobalMapToolbarVM>), nameof(GlobalMapToolbarView<GlobalMapToolbarVM>.DestroyViewImplementation))]
        [HarmonyPrefix]
        public static void GlobalMapToolbarView_DestroyViewImplementation_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnGlobalMapDisposed();
        }

        [HarmonyPatch(typeof(GlobalMapToolbarView<GlobalMapToolbarVM>), nameof(GlobalMapToolbarView<GlobalMapToolbarVM>.OnSkipDay))]
        [HarmonyPrefix]
        public static void GlobalMapToolbarView_OnSkipDay_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnGlobalMapSkipDay();
        }

        [HarmonyPatch(typeof(GlobalMapToolbarSettingsPCView), nameof(GlobalMapToolbarSettingsPCView.BindViewImplementation))]
        [HarmonyPostfix]
        public static void GlobalMapToolbarSettingsPCView_BindViewImplementation_Postfix(GlobalMapToolbarSettingsPCView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            __instance.m_AutoTacticalCombat.m_Button.Interactable = Main.Multiplayer.CanNavigateOnGlobalMap();
        }

        [HarmonyPatch(typeof(GlobalMapToolbarSettingsVM), nameof(GlobalMapToolbarSettingsVM.SwitchAutoTacticalCombat))]
        [HarmonyPostfix]
        public static void GlobalMapToolbarSettingsVM_SwitchAutoTacticalCombat_Postfix(GlobalMapToolbarSettingsVM __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var isEnabled = __instance.UISettings.AutoTacticalCombat;
            Main.Multiplayer.OnGlobalMapAutoCrusadeCombatChanged(isEnabled);
        }

        [HarmonyPatch(typeof(GlobalMapCrusadeArmyVM), nameof(GlobalMapCrusadeArmyVM.OnSelectClick))]
        [HarmonyPrefix]
        public static void GlobalMapCrusadeArmyVM_OnSelectClick_Prefix(GlobalMapCrusadeArmyVM __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var armyId = __instance.Army?.Id;
            Main.Multiplayer.OnGlobalMapSelectedArmyChanged(armyId);
        }

        private static void OnArmyPawnClicked(GlobalMapArmyPawn armyPawn)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var armyId = armyPawn.State.Id;
            Main.Multiplayer.OnGlobalMapSelectedArmyChanged(armyId);
        }

        private static void OnPlayerPawnClicked()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnGlobalMapSelectedArmyChanged(null);
        }

        private static NetworkGlobalMapTravelerMode GetTravelerMode(bool state)
        {
            if (state)
            {
                return NetworkGlobalMapTravelerMode.Army;
            }

            return NetworkGlobalMapTravelerMode.Player;
        }
    }
}
