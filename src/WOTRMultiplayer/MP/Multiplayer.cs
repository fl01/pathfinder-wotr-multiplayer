using Kingmaker.UI.Common;
using Kingmaker.UI.MVVM._PCView.ContextMenu;
using Kingmaker.UI.MVVM._VM.ContextMenu;
using UnityEngine;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.UI.Controllers.Menu;
using WOTRMultiplayer.UI;

namespace WOTRMultiplayer.MP
{
    public class Multiplayer : IMultiplayer
    {
        private UI.Menu.MultiplayerWindow _multiplayerWindow;
        private readonly IHostMenuItemController _hostMenuItemController;
        private readonly IJoinMenuItemController _joinMenuItemController;
        private readonly IMultiplayerClient _multiplayerClient;
        private readonly IMultiplayerHost _multiplayerHost;

        public IUIFactory Factory { get; private set; }

        public Multiplayer(
            IUIFactory uiFactory,
            IHostMenuItemController hostMenuItemController,
            IJoinMenuItemController joinMenuItemController,
            IMultiplayerHost multiplayerHost,
            IMultiplayerClient multiplayerClient)
        {
            Factory = uiFactory;
            _multiplayerHost = multiplayerHost;
            _multiplayerClient = multiplayerClient;
            _hostMenuItemController = hostMenuItemController;
            _joinMenuItemController = joinMenuItemController;
        }

        public bool InjectMultiplayerMenuWindow(GameObject menuItemPrototype, Transform parent)
        {
            var multiplayerMenu = UnityEngine.Object.Instantiate(menuItemPrototype, parent);
            multiplayerMenu.transform.SetSiblingIndex(menuItemPrototype.transform.GetSiblingIndex());
            var multiplayerMenuView = multiplayerMenu.GetComponent<ContextMenuEntityPCView>();
            var element = Factory.CreateCopyOfCreditsScreen();
            _multiplayerWindow = element.AddComponent<UI.Menu.MultiplayerWindow>();
            _multiplayerWindow.AssignMenuItemControllers(_hostMenuItemController, _joinMenuItemController);
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
