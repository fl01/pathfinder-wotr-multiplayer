using System.Numerics;
using Kingmaker.Controllers.Clicks.Handlers;
using Kingmaker.EntitySystem.Entities;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Windows;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.UI.Lobby;

namespace WOTRMultiplayer.MP
{
    public class Multiplayer : IMultiplayer
    {
        private IMultiplayerWindow _multiplayerWindow;
        private ILobbyWindow _lobbyWindow;

        private readonly ILobbyWindowController _lobbyWindowController;
        private readonly IMultiplayerClient _multiplayerClient;
        private readonly IMultiplayerHost _multiplayerHost;
        private readonly ILogger _logger;

        public IUIFactory Factory { get; private set; }

        public bool IsActive => _multiplayerClient.IsActive || _multiplayerHost.IsActive;

        public Multiplayer(
            ILogger<Multiplayer> logger,
            IUIFactory uiFactory,
            ILobbyWindowController lobbyWindowController,
            IMultiplayerHost multiplayerHost,
            IMultiplayerClient multiplayerClient)
        {
            _logger = logger;
            Factory = uiFactory;
            _multiplayerHost = multiplayerHost;
            _multiplayerClient = multiplayerClient;
            _lobbyWindowController = lobbyWindowController;
        }

        public bool InitializeMultiplayer(InitializeMultiplayerContext context)
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

            _multiplayerWindow = Factory.InitializeMultiplayerWindow(context, ShowMultiplayerWindow);

            return true;
        }

        public void TerminateMultiplayer()
        {
            _logger.LogInformation("Disposing both multiplayer host/client");
            _multiplayerHost.Dispose();
            _multiplayerClient.Dispose();
            _lobbyWindowController.ResetOwnerContent(LobbyWindowOwner.EscMenu);
            _logger.LogInformation("Disposing Esc menu window game objects");
            Factory.DestroyLobbyWindow(_lobbyWindow);
        }

        public void InitializeEscMenuLobbyWindow(InitializeEscMenuLobbyWindowContext context)
        {
            _logger.LogInformation("Creating Esc menu lobby item");
            _lobbyWindow = Factory.InitializeEscMenuLobbyWindow(context, _multiplayerHost.IsActive, ShowEscMenuMultiplayerLobby);

            _lobbyWindow.NetworkGame = () => _multiplayerHost.IsActive ? _multiplayerHost.CurrentGame : _multiplayerClient.CurrentGame;
            _lobbyWindow.AssignLobbyController(_lobbyWindowController);
        }

        public void MoveCharacter(UnitEntityData unit, ClickGroundHandler.CommandSettings settings)
        {
            var destination = new Vector3(settings.Destination.x, settings.Destination.y, settings.Destination.z);
            if (_multiplayerClient.IsActive)
            {
                _logger.LogInformation("MultiplayerClient is active. Moving character. Name={characterName}, Destination={destination}, Delay={delay}, Orientation={orientation}", unit.CharacterName, destination, settings.Delay, settings.Orientation);
                _multiplayerClient.MoveCharacter(unit.CharacterName, destination, settings.Delay, settings.Orientation);
                return;
            }

            _logger.LogInformation("MultiplayerHost is active. Moving character. Name={characterName}, Destination={destination}, Delay={delay}, Orientation={orientation}", unit.CharacterName, destination, settings.Delay, settings.Orientation);
            _multiplayerHost.MoveCharacter(unit.CharacterName, destination, settings.Delay, settings.Orientation);
        }

        public bool CanControlCharacter(string characterName)
        {
            return true;
        }

        private void ShowEscMenuMultiplayerLobby()
        {
            _logger.LogInformation("Show lobby window");
            _lobbyWindow.Show(true);
        }

        private void ShowMultiplayerWindow()
        {
            _logger.LogInformation("Show Multiplayer window");
            _multiplayerWindow.Show(true);
        }
    }
}
