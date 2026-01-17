using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Globalmap;
using Kingmaker.Globalmap.State;
using Kingmaker.Globalmap.View;
using Kingmaker.UI.MVVM._PCView.GlobalMap.Toolbar;
using Kingmaker.UI.MVVM._VM.Crusade.PointerMarker;
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
        [HarmonyPatch(typeof(GlobalMapArmyPointerMarkerEntityVM), nameof(GlobalMapArmyPointerMarkerEntityVM.OnLeftClick))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> GlobalMapArmyPointerMarkerEntityVM_OnLeftClick_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.Method(typeof(GlobalMapController), nameof(GlobalMapController.SetSelectedArmy));
            var extraCall = AccessTools.Method(typeof(GlobalMapControlPatches), nameof(GlobalMapControlPatches.OnSelectMarkerArmy));
            var matcher = new CodeMatcher(instructions);

            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<GlobalMapControlPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var newInstructions = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, extraCall),
            };
            match = match.Advance(-4).RemoveInstructions(5).Insert(newInstructions);
            Main.GetLogger<GlobalMapControlPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        public static void OnSelectMarkerArmy(GlobalMapArmyPointerMarkerEntityVM pointerMarkerEntityVM)
        {
            if (!Main.Multiplayer.IsActive || Main.Multiplayer.CanNavigateOnGlobalMap())
            {
                var army = pointerMarkerEntityVM.ArmyState;
                Game.Instance.GlobalMapController.SetSelectedArmy(army);
            }
        }

        [HarmonyPatch(typeof(GlobalMapSelectController), nameof(GlobalMapSelectController.HandleClick), [typeof(GlobalMapPawn)])]
        [HarmonyPrefix]
        public static bool GlobalMapSelectController_HandleClick_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.CanNavigateOnGlobalMap();
            return canContinue;
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

        [HarmonyPatch(typeof(GlobalMapController), nameof(GlobalMapController.SetSelectedArmy))]
        [HarmonyPrefix]
        public static void GlobalMapCrusadeArmyVM_OnSelectClick_Prefix(GlobalMapArmyState army)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var armyId = army?.Id;
            Main.Multiplayer.OnGlobalMapSelectedArmyChanged(armyId);
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
