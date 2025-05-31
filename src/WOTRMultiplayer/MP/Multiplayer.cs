using Kingmaker.UI.Common;
using Kingmaker.UI.MVVM._PCView.ContextMenu;
using Kingmaker.UI.MVVM._VM.ContextMenu;
using Microsoft.Extensions.Logging;
using UnityEngine;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.UI.Controllers.Menu;
using WOTRMultiplayer.Abstractions.Unity;
using WOTRMultiplayer.UI;
using WOTRMultiplayer.UI.Menu.Windows;

namespace WOTRMultiplayer.MP
{
    public class Multiplayer : IMultiplayer
    {
        private MultiplayerWindow _multiplayerWindow;
        private readonly IHostMenuItemController _hostMenuItemController;
        private readonly IJoinMenuItemController _joinMenuItemController;
        private readonly IMultiplayerClient _multiplayerClient;
        private readonly IMultiplayerHost _multiplayerHost;
        private readonly IPortraitProvider _portraitProvider;
        private readonly Microsoft.Extensions.Logging.ILogger _logger;

        public IUIFactory Factory { get; private set; }

        public bool IsActive => _multiplayerClient.IsActive || _multiplayerHost.IsActive;

        private readonly IMainThreadAccessor _mainThreadAccessor;

        public Multiplayer(
            Microsoft.Extensions.Logging.ILogger<Multiplayer> logger,
            IUIFactory uiFactory,
            IMainThreadAccessor mainThreadAccessor,
            IHostMenuItemController hostMenuItemController,
            IJoinMenuItemController joinMenuItemController,
            IPortraitProvider portraitProvider,
            IMultiplayerHost multiplayerHost,
            IMultiplayerClient multiplayerClient)
        {
            _logger = logger;
            Factory = uiFactory;
            _mainThreadAccessor = mainThreadAccessor;
            _multiplayerHost = multiplayerHost;
            _multiplayerClient = multiplayerClient;
            _hostMenuItemController = hostMenuItemController;
            _joinMenuItemController = joinMenuItemController;
            _portraitProvider = portraitProvider;
        }

        public bool InitializeMultiplayer(GameObject menuItemPrototype, Transform parent)
        {
            _portraitProvider.Initialize();
            _multiplayerHost.Dispose();
            _multiplayerClient.Dispose();

            var multiplayerMenu = UnityEngine.Object.Instantiate(menuItemPrototype, parent);
            multiplayerMenu.transform.SetSiblingIndex(menuItemPrototype.transform.GetSiblingIndex());
            var multiplayerMenuView = multiplayerMenu.GetComponent<ContextMenuEntityPCView>();
            var element = Factory.CreateCopyOfCreditsScreen();
            _multiplayerWindow = element.AddComponent<MultiplayerWindow>();
            _mainThreadAccessor.SetQueue(_multiplayerWindow.MainThreadQueue);
            _multiplayerWindow.AssignMenuItemControllers(_hostMenuItemController, _joinMenuItemController);
            _multiplayerWindow.Initialize();

            Factory.CreateBackgroundArt(_multiplayerWindow.transform.Find("BackgroundGroup"));
            var text = UIUtility.GetSaberBookFormat(UIStringConsts.MainMenu.MultiplayerMenu);
            var viewModel = new ContextMenuEntityVM(new ContextMenuCollectionEntity(UIUtility.GetSaberBookFormat(text), ShowMultiplayerWindow));
            multiplayerMenuView.Bind(viewModel);

            _multiplayerWindow.OnDispose = () =>
            {
                _logger.LogInformation("Multiplayer window is disposing.");
                viewModel.Dispose();
                multiplayerMenuView.Unbind();
            };

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
