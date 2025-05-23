using System;
using Kingmaker.UI.Common;
using Kingmaker.UI.MVVM._PCView.ContextMenu;
using Kingmaker.UI.MVVM._VM.ContextMenu;
using UnityEngine;
using WOTRMultiplayer.Strings;
using WOTRMultiplayer.UI.Menu;

namespace WOTRMultiplayer
{
    public class Multiplayer : IDisposable
    {
        private MultiplayerWindow _multiplayerWindow;

        public UIElementFactory ElementFactory { get; private set; }

        public Multiplayer(UIElementFactory uiElementFactory)
        {
            ElementFactory = uiElementFactory;
        }

        public bool InjectMultiplayerMenuWindow(GameObject menuItemPrototype, Transform parent)
        {
            var multiplayerMenu = UnityEngine.Object.Instantiate(menuItemPrototype, parent);
            multiplayerMenu.transform.SetSiblingIndex(3);
            var multiplayerMenuView = multiplayerMenu.GetComponent<ContextMenuEntityPCView>();
            var element = ElementFactory.CreateCopyOfCreditsScreen();
            _multiplayerWindow = element.AddComponent<MultiplayerWindow>();
            _multiplayerWindow.Initialize();
            var text = UIUtility.GetSaberBookFormat(StringConsts.MainMenu.MultiplayerMenu);
            var viewModel = new ContextMenuEntityVM(new ContextMenuCollectionEntity(UIUtility.GetSaberBookFormat(text), ShowMultiplayerWindow));
            multiplayerMenuView.Bind(viewModel);
            return true;
        }

        private void ShowMultiplayerWindow()
        {
            _multiplayerWindow.Show(true);
        }

        public void Dispose()
        {
        }
    }
}
