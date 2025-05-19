using HarmonyLib;
using Kingmaker;
using Kingmaker.UI.Common;
using Kingmaker.UI.MVVM._PCView.ContextMenu;
using Kingmaker.UI.MVVM._PCView.MainMenu;
using Kingmaker.UI.MVVM._VM.ContextMenu;
using UnityEngine;
using WOTRMultiplayer.Strings;

namespace WOTRMultiplayer.Menu
{
    [HarmonyPatch]
    public class MenuPatches
    {
        [HarmonyPatch(typeof(MainMenuPCView), "BindViewImplementation")]
        [HarmonyPrefix]
        public static void MainMenuPCView_BindViewImplementation_Prefix(MainMenuPCView __instance)
        {
            Logging.Logger.Info("Applying");
            var menuButtons = __instance.m_MainMenuSideBarPCView.transform.GetChild(0);
            var menuItemToCopy = menuButtons.GetChild(3).gameObject;
            var multiplayerMenu = Object.Instantiate(menuItemToCopy, menuButtons.transform);
            multiplayerMenu.transform.SetSiblingIndex(3);
            var multiplayerMenuView = multiplayerMenu.GetComponent<ContextMenuEntityPCView>();
            var text = UIUtility.GetSaberBookFormat(StringsConst.MainMenu.MultiplayerMenu);
            multiplayerMenuView.m_Label.text = text;
            var viewModel = new ContextMenuEntityVM(new ContextMenuCollectionEntity(UIUtility.GetSaberBookFormat(text), __instance.OpenCredits));
            var copy = Object.Instantiate(Game.Instance.UI.CreditsUI.gameObject);
            multiplayerMenuView.Bind(viewModel);
        }
    }
}
