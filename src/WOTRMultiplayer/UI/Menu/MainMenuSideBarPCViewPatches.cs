using System;
using System.ComponentModel;
using HarmonyLib;
using Kingmaker.UI.MVVM;
using Kingmaker.UI.MVVM._PCView.Common;
using Kingmaker.UI.MVVM._PCView.MainMenu;
using Kingmaker.UI.MVVM._PCView.Settings;
using TMPro;

namespace WOTRMultiplayer.UI.Menu
{
    [HarmonyPatch]
    public class MainMenuSideBarPCViewPatches
    {
        [HarmonyPatch(typeof(SettingsPCView), "Initialize")]
        [HarmonyPostfix]
        public static void SettingsEntityDropdownPCView_BindViewImplementation_Prefix(SettingsPCView __instance)
        {
            Logging.Logger.Warning("Applying");
            Main.Multiplayer.Factory.StoreDropdownPrefab(__instance.m_SettingsViews.m_SettingsEntityDropdownViewPrefab);
        }


        [HarmonyPatch(typeof(MainMenuSideBarPCView), "BindViewImplementation")]
        [HarmonyPrefix]
        public static void MainMenuSideBarPCView_BindViewImplementation_Prefix(MainMenuSideBarPCView __instance)
        {
            Logging.Logger.Info("Applying");
            try
            {
                var commonPCView = (RootUIContext.Instance.m_CommonView as CommonPCView)?.m_SaveLoadPCView;
                Main.Multiplayer.Factory.StoreSaveLoadPCViewPrefab(commonPCView);
                if (commonPCView != null)
                {
                    var screen = commonPCView.gameObject.transform.Find("SaveLoadScreen");
                    var saveList = screen.Find("SaveSlotCollectionPlace").Find("SaveSlotVirtualCollectionView");
                    Main.Multiplayer.Factory.StoreTopDecoration(saveList.Find("decoration").gameObject);
                    Main.Multiplayer.Factory.StoreBottomDecoration(saveList.Find("Decoration").gameObject);

                    var title = screen.Find("SaveLoadDetails").Find("Title");
                    var defaultTextMesh = title.GetComponentInChildren<TextMeshProUGUI>();
                    Main.Multiplayer.Factory.StoreDefaultTextMesh(defaultTextMesh);
                }

                var menuButtons = __instance.transform.GetChild(0);
                var isOk = Main.Multiplayer.InjectMultiplayerMenuWindow(menuButtons.GetChild(3).gameObject, menuButtons.transform);
                if (!isOk)
                {
                    Logging.Logger.Error("Unable to inject multiplayer menu");
                }
            }
            catch (Exception ex)
            {
                Logging.Logger.Error("Unable to apply patch", ex);
                throw;
            }
        }
    }
}
