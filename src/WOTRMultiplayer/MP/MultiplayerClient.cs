using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Messages.Lobby;

namespace WOTRMultiplayer.MP
{
    public class MultiplayerClient : IMultiplayerClient
    {
        private readonly ILogger<MultiplayerClient> _logger;
        private readonly IIPEndPointParser _ipEndPointParser;
        private readonly INetworkServerClient _networkServerClient;
        private readonly ILobbyWindowController _lobbyWindowController;

        private readonly List<NetworkPlayer> _playersList = [];
        private readonly object _actionlock = new();
        private const int LocalPlayerId = -2;

        public bool IsActive => _networkServerClient.IsActive;

        public MultiplayerClient(
            ILogger<MultiplayerClient> logger,
            IIPEndPointParser ipEndPointParser,
            ILobbyWindowController lobbyWindowController,
            INetworkServerClient networkServerClient)
        {
            _logger = logger;
            _ipEndPointParser = ipEndPointParser;
            _networkServerClient = networkServerClient;
            _lobbyWindowController = lobbyWindowController;
        }

        public JoinLobbyResult Join(string address, MultiplayerSettings settings)
        {
            if (_networkServerClient.IsActive)
            {
                _networkServerClient.Dispose();
            }

            RegisterHandlers();

            if (!_ipEndPointParser.TryParse(address, out IPEndPoint endpoint))
            {
                return JoinLobbyResult.Error("Unable to parse provided IP address. Please verify your input");
            }

            if (endpoint.Port == 0)
            {
                return JoinLobbyResult.Error("Provided port is invalid. Please verify your input");
            }

            _networkServerClient.ConnectAsync(endpoint.Address.ToString(), endpoint.Port).Wait();
            _playersList.Add(new NetworkPlayer(LocalPlayerId) { Name = settings.PlayerName });
            return JoinLobbyResult.Ok();
        }

        public bool ReadyChanged()
        {
            var player = _playersList.First(p => p.Id == LocalPlayerId); // client should be always present
            player.IsReady = !player.IsReady;
            var readyChanged = new PlayerReadyStatusChanged { IsReady = player.IsReady };
            _networkServerClient.SendAsync(readyChanged).Wait();
            return readyChanged.IsReady;
        }

        private void RegisterHandlers()
        {
            _networkServerClient
                .Register<PlayerNameRequest>(OnPlayerNameRequest)
                ;
        }

        private void OnPlayerNameRequest(PlayerNameRequest request)
        {
            _logger.LogInformation("Player name requested");
            var nameResponse = new PlayerNameResponse() { Name = "AAA" };
            _networkServerClient.SendAsync(nameResponse).Wait();
        }

        public void Dispose()
        {
            _playersList.Clear();
            _networkServerClient?.Dispose();
        }
    }
}
