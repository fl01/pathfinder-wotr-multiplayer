using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Messages.Lobby;
using WOTRMultiplayer.UI;

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

        public Action<string> OnNetworkError { get; set; }

        public Action OnConnected { get; set; }
        public Action OnDisconnected { get; set; }

        public bool IsActive => _networkServerClient.IsActive;

        public bool IsConnecting => _networkServerClient.IsConnecting;

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

            if (!_ipEndPointParser.TryParse(address, out IPEndPoint endpoint))
            {
                return JoinLobbyResult.Error(StringConsts.MultiplayerClient.Errors.InvalidIP);
            }

            if (endpoint.Port <= 0 || endpoint.Port > ushort.MaxValue)
            {
                return JoinLobbyResult.Error(StringConsts.MultiplayerClient.Errors.InvalidPort);
            }

            RegisterHandlers();

            _networkServerClient.ConnectAsync(endpoint.Address.ToString(), endpoint.Port);

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

            _networkServerClient.OnError = OnNetworkClientError;
            _networkServerClient.OnConnected = OnNetworkClientConnected;
            _networkServerClient.OnDisconnected = OnNetworkClientDisconnected;
        }

        private void OnNetworkClientDisconnected()
        {
            _logger.LogInformation("Client disconnected");
        }

        private void OnNetworkClientConnected(EndPoint endpoint)
        {
            _lobbyWindowController.UpdateServerInfo(endpoint.ToString());
            OnConnected?.Invoke();
        }

        private void OnNetworkClientError(Exception exception)
        {
            if (exception is SocketException socketException)
            {
                var code = socketException.ErrorCode;
                var errorText = $"Network error occurred. Error code: {(SocketError)code}";
                OnNetworkError?.Invoke(errorText);
                return;
            }

            OnNetworkError?.Invoke("Generic network error occurred");
            _logger.LogError(exception, "Generic network error occurred");
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
