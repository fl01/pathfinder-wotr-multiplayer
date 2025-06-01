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
using WOTRMultiplayer.Abstractions.Unity;
using WOTRMultiplayer.UI;
using WOTRMultiplayer.UI.Lobby;
using WOTRMultiplayer.UI.Menu.Windows;

namespace WOTRMultiplayer.MP
{
    public class Multiplayer : IMultiplayer
    {
        private MultiplayerWindow _multiplayerWindow;
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

        private readonly IMainThreadAccessor _mainThreadAccessor;

        public Multiplayer(
            ILogger<Multiplayer> logger,
            IServiceProvider serviceProvider,
            IUIFactory uiFactory,
            ILobbyWindowController lobbyWindowController,
            IMainThreadAccessor mainThreadAccessor,
            IHostMenuItemController hostMenuItemController,
            IJoinMenuItemController joinMenuItemController,
            IMultiplayerHost multiplayerHost,
            IMultiplayerClient multiplayerClient)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            Factory = uiFactory;
            _mainThreadAccessor = mainThreadAccessor;
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
        }

        public void Dispose()
        {
            _multiplayerWindow?.Dispose();
            _lobbyWindow?.Dispose();
        }

        public void CreateEscMenuItem(EscMenuPCView view)
        {
            var (menuItem, windowContainer) = Factory.CreateEscMenuItem(view);

            _lobbyWindow = windowContainer.AddComponent<LobbyWindow>();
            _lobbyWindow.SetLogger(_serviceProvider.GetService<ILogger<LobbyWindow>>());
            _lobbyWindow.OnClose = CloseMP;
            windowContainer.AddComponent<Image>().color = Color.green;
            windowContainer.SetActive(false);

            var button = menuItem.GetComponent<OwlcatButton>();
            button.OnLeftClick.RemoveAllListeners();
            button.OnLeftClick.AddListener(() => ShowEscMenuMultiplayerLobby(windowContainer));
        }

        private void CloseMP()
        {
            _logger.LogInformation("Closing");
            _lobbyWindowController.ResetData();
        }

        private void ShowEscMenuMultiplayerLobby(GameObject windowContainer)
        {
            _lobbyWindowController.SetActiveOwner(LobbyWindowOwner.EscMenu);
            _lobbyWindowController.InitializeContent(LobbyWindowOwner.EscMenu, windowContainer.transform, _multiplayerHost.IsActive);

            _logger.LogInformation("Updaing lobby info");
            var game = _multiplayerClient.IsActive ? _multiplayerClient.CurrentGame : _multiplayerHost.CurrentGame;
            _lobbyWindowController.UpdateServerInfo(game.Endpoint.ToString());
            _lobbyWindowController.UpdatePlayers(game.Players);
            _lobbyWindowController.UpdatePortraits(game.Portraits);
            _logger.LogInformation("Show lobby");
            _lobbyWindow.Show(true);
        }

        private void ShowMultiplayerWindow()
        {
            _multiplayerWindow.Show(true);
        }
    }
}
