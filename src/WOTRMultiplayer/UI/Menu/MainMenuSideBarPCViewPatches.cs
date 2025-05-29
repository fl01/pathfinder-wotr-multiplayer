using System;
using HarmonyLib;
using Kingmaker;
using Kingmaker.UI.MVVM;
using Kingmaker.UI.MVVM._PCView.Common;
using Kingmaker.UI.MVVM._PCView.MainMenu;
using Kingmaker.UI.MVVM._PCView.Settings;
using Serilog;
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
            try
            {
                Log.Logger.Information("Applying");
                Main.Multiplayer.Factory.StoreDropdownPrefab(__instance.m_SettingsViews.m_SettingsEntityDropdownViewPrefab);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Unable to apply SettingsPCView patch");
                throw;
            }
        }


        [HarmonyPatch(typeof(MainMenuSideBarPCView), "BindViewImplementation")]
        [HarmonyPrefix]
        public static void MainMenuSideBarPCView_BindViewImplementation_Prefix(MainMenuSideBarPCView __instance)
        {
            Log.Logger.Information("Applying");
            try
            {
                var commonPCView = (RootUIContext.Instance.m_CommonView as CommonPCView)?.m_SaveLoadPCView;
                Main.Multiplayer.Factory.StoreSaveLoadPCViewPrefab(commonPCView);
                var creditsSearchPanel = Game.Instance.UI.CreditsUI.transform.Find("CreditsScreen").Find("SearchPanel");
                Main.Multiplayer.Factory.StoreInputPrefab(creditsSearchPanel.Find("Input_Field").gameObject);
                Main.Multiplayer.Factory.StoreButtonPrefab(creditsSearchPanel.Find("SearchButton").gameObject);
                if (commonPCView != null)
                {
                    var screen = commonPCView.gameObject.transform.Find("SaveLoadScreen");
                    var saveList = screen.Find("SaveSlotCollectionPlace").Find("SaveSlotVirtualCollectionView");
                    Main.Multiplayer.Factory.StoreBorderDecoration(saveList.Find("Decoration").gameObject);

                    Main.Multiplayer.Factory.StoreBackgroundArt(screen.Find("PapperBackground").gameObject);

                    var title = screen.Find("SaveLoadDetails").Find("Title");
                    var defaultTextMesh = title.GetComponentInChildren<TextMeshProUGUI>();
                    Main.Multiplayer.Factory.StoreDefaultTextMesh(defaultTextMesh);
                }

                var menuButtons = __instance.transform.GetChild(0);
                for (int menuIndex = 0; menuIndex >= 0; menuIndex++)
                {
                    var menuButton = menuButtons.GetChild(menuIndex).gameObject;
                    if (string.Equals(menuButton.name, "Settings"))
                    {
                        var isOk = Main.Multiplayer.InjectMultiplayerMenuWindow(menuButton, menuButtons.transform);
                        if (!isOk)
                        {
                            Log.Logger.Error("Unable to inject multiplayer menu");
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Unable to apply MainMenuSideBarPCView patch");
                throw;
            }
        }
    }
}
