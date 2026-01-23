using HarmonyLib;
using Kingmaker;
using Kingmaker.Globalmap;
using Kingmaker.Globalmap.State;
using Kingmaker.Globalmap.View;
using Kingmaker.UI.MVVM._PCView.GlobalMap.Toolbar;
using Kingmaker.UI.MVVM._VM.Crusade.PointerMarker;
using Kingmaker.UI.MVVM._VM.GlobalMap;
using Kingmaker.UI.MVVM._VM.GlobalMap.Toolbar;
using UniRx;
using WOTRMultiplayer.Entities.GlobalMap;

namespace WOTRMultiplayer.HarmonyPatches.GlobalMap
{
    [HarmonyPatch]
    public class GlobalMapControlPatches
    {
        [HarmonyPatch(typeof(GlobalMapArmyPointerMarkerEntityVM), nameof(GlobalMapArmyPointerMarkerEntityVM.OnLeftClick))]
        [HarmonyPrefix]
        public static bool GlobalMapArmyPointerMarkerEntityVM_OnLeftClick_Prefix(GlobalMapArmyPointerMarkerEntityVM __instance)
        {
            if (!Main.Multiplayer.IsActive || Main.Multiplayer.CanNavigateOnGlobalMap())
            {
                return true;
            }

            Game.Instance.UI.GetCameraRig().ScrollTo(__instance.Position.Value, false);
            return false;
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

            var globalMapArmy = Create(army);
            Main.Multiplayer.OnGlobalMapSelectedArmyChanged(globalMapArmy);
        }

        private static NetworkGlobalMapTravelerMode GetTravelerMode(bool state)
        {
            if (state)
            {
                return NetworkGlobalMapTravelerMode.Army;
            }

            return NetworkGlobalMapTravelerMode.Player;
        }

        private static NetworkGlobalMapArmy Create(GlobalMapArmyState army)
        {
            if (army == null)
            {
                return null;
            }

            var globalMapArmy = new NetworkGlobalMapArmy
            {
                Id = army.Id
            };

            return globalMapArmy;
        }
    }
}
