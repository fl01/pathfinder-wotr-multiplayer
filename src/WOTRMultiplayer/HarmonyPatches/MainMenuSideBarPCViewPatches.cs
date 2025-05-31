using System;
using HarmonyLib;
using Kingmaker;
using Kingmaker.UI.MVVM._PCView.Common;
using Kingmaker.UI.MVVM._PCView.MainMenu;
using Microsoft.Extensions.Logging;
using TMPro;

namespace WOTRMultiplayer.HarmonyPatches
{
    [HarmonyPatch]
    public class MainMenuSideBarPCViewPatches
    {
        [HarmonyPatch(typeof(MainMenuSideBarPCView), "BindViewImplementation")]
        [HarmonyPrefix]
        public static void MainMenuSideBarPCView_BindViewImplementation_Prefix(MainMenuSideBarPCView __instance)
        {
            var logger = Main.GetLogger<MainMenuSideBarPCViewPatches>();
            logger.LogInformation("Applying patch [{patchName}]", nameof(MainMenuSideBarPCView_BindViewImplementation_Prefix));

            try
            {

                var commonPCView = (Game.Instance.RootUiContext.m_CommonView as CommonPCView)?.m_SaveLoadPCView;
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
                for (int menuIndex = 0; menuIndex < menuButtons.childCount; menuIndex++)
                {
                    var menuButton = menuButtons.GetChild(menuIndex).gameObject;
                    if (string.Equals(menuButton.name, "Settings"))
                    {
                        var isOk = Main.Multiplayer.InitializeMultiplayer(menuButton, menuButtons.transform);
                        if (!isOk)
                        {
                            logger.LogError("Unable to inject multiplayer menu");
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to apply MainMenuSideBarPCView patch");
                throw;
            }
        }
    }
}
