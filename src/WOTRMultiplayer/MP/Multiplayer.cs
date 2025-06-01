using System;
using Kingmaker.UI.Common;
using Kingmaker.UI.MVVM._PCView.ContextMenu;
using Kingmaker.UI.MVVM._PCView.EscMenu;
using Kingmaker.UI.MVVM._VM.ContextMenu;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.Controls.Button;
using UnityEngine;
using UnityEngine.UI;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Controllers.Menu;
using WOTRMultiplayer.UI;
using WOTRMultiplayer.UI.Lobby;
using WOTRMultiplayer.UI.Menu.Windows;

namespace WOTRMultiplayer.MP
{
    public class Multiplayer : IMultiplayer
    {
        private MultiplayerWindow _multiplayerWindow;
        private GameObject _lobbyMenuItem;
        private LobbyWindow _lobbyWindow;

        private readonly IHostMenuItemController _hostMenuItemController;
        private readonly IJoinMenuItemController _joinMenuItemController;
        private readonly ILobbyWindowController _lobbyWindowController;
        private readonly IMultiplayerClient _multiplayerClient;
        private readonly IMultiplayerHost _multiplayerHost;
        private readonly Microsoft.Extensions.Logging.ILogger _logger;
        private readonly IServiceProvider _serviceProvider;

        public IUIFactory Factory { get; private set; }

        public bool IsActive => _multiplayerClient.IsActive || _multiplayerHost.IsActive;

        public Multiplayer(
            ILogger<Multiplayer> logger,
            IServiceProvider serviceProvider,
            IUIFactory uiFactory,
            ILobbyWindowController lobbyWindowController,
            IHostMenuItemController hostMenuItemController,
            IJoinMenuItemController joinMenuItemController,
            IMultiplayerHost multiplayerHost,
            IMultiplayerClient multiplayerClient)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            Factory = uiFactory;
            _multiplayerHost = multiplayerHost;
            _multiplayerClient = multiplayerClient;
            _hostMenuItemController = hostMenuItemController;
            _joinMenuItemController = joinMenuItemController;
            _lobbyWindowController = lobbyWindowController;
        }

        public bool InitializeMultiplayer(GameObject menuItemPrototype, Transform parent)
        {
            if (_multiplayerHost.IsActive)
            {
                _logger.LogWarning("Multiplayer host has not been properly disposed. Verify exit game/main menu handles");
                _multiplayerHost.Dispose();
            }

            if (_multiplayerClient.IsActive)
            {
                _logger.LogWarning("Multiplayer client has not been properly disposed. Verify exit game/main menu handlers");
                _multiplayerClient.Dispose();
            }

            var multiplayerMenu = UnityEngine.Object.Instantiate(menuItemPrototype, parent);
            multiplayerMenu.transform.SetSiblingIndex(menuItemPrototype.transform.GetSiblingIndex());
            var multiplayerMenuView = multiplayerMenu.GetComponent<ContextMenuEntityPCView>();
            var element = Factory.CreateCopyOfCreditsScreen();
            _multiplayerWindow = element.AddComponent<MultiplayerWindow>();
            _multiplayerWindow.SetLogger(_serviceProvider.GetService<ILogger<MultiplayerWindow>>());
            _multiplayerWindow.AssignMenuItemControllers(_hostMenuItemController, _joinMenuItemController);
            _multiplayerWindow.Initialize();

            Factory.CreateBackgroundArt(_multiplayerWindow.transform.Find("BackgroundGroup"));
            var text = UIUtility.GetSaberBookFormat(UIStringConsts.MainMenu.MultiplayerMenu);
            var viewModel = new ContextMenuEntityVM(new ContextMenuCollectionEntity(UIUtility.GetSaberBookFormat(text), ShowMultiplayerWindow));
            multiplayerMenuView.Bind(viewModel);

            return true;
        }

        public void TerminateMultiplayer()
        {
            _logger.LogInformation("Disposing both multiplayer host/client");
            _multiplayerHost.Dispose();
            _multiplayerClient.Dispose();
            _lobbyWindowController.ResetOwnerContent(LobbyWindowOwner.EscMenu);
            _logger.LogInformation("Disposing Esc menu window game objects");
            Factory.DestroyImmediate(_lobbyWindow?.gameObject);
            Factory.DestroyImmediate(_lobbyMenuItem?.gameObject);
        }

        public void CreateEscMenuItem(EscMenuPCView view)
        {
            _logger.LogInformation("Creating Esc menu lobby item");
            var (menuItem, windowContainer) = Factory.CreateEscMenuItem(view);

            _lobbyMenuItem = menuItem;
            _lobbyWindow = windowContainer.AddComponent<LobbyWindow>();
            _lobbyWindow.SetLogger(_serviceProvider.GetService<ILogger<LobbyWindow>>());
            _lobbyWindow.AssignLobbyController(_lobbyWindowController);
            _lobbyWindowController.InitializeContent(LobbyWindowOwner.EscMenu, windowContainer.transform, _multiplayerHost.IsActive);
            _lobbyWindow.NetworkGame = () => _multiplayerHost.IsActive ? _multiplayerHost.CurrentGame : _multiplayerClient.CurrentGame;
            windowContainer.AddComponent<Image>().color = Color.green;
            windowContainer.SetActive(false);

            var button = _lobbyMenuItem.GetComponent<OwlcatButton>();
            button.OnLeftClick.RemoveAllListeners();
            button.OnLeftClick.AddListener(ShowEscMenuMultiplayerLobby);
        }

        private void ShowEscMenuMultiplayerLobby()
        {
            _logger.LogInformation("Show lobby window");
            _lobbyWindow.Show(true);
        }

        private void ShowMultiplayerWindow()
        {
            _multiplayerWindow.Show(true);
        }
    }
}
