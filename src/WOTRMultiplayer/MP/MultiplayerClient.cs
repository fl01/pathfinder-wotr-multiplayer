using System;
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

        private NetworkGame _game;
        private readonly object _actionlock = new();
        private long _localPlayerId;

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
                Dispose();
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

            return JoinLobbyResult.Ok();
        }

        public bool ReadyChanged()
        {
            var player = _game.Players.First(p => p.Id == _localPlayerId); // local client should be always present
            player.IsReady = !player.IsReady;
            var readyChanged = new PlayerReadyStatusChanged { PlayerId = player.Id, IsReady = player.IsReady };
            _networkServerClient.SendAsync(readyChanged).Wait();
            return readyChanged.IsReady;
        }

        private void RegisterHandlers()
        {
            _networkServerClient
                .Register<PlayerNameRequest>(OnPlayerNameRequest)
                .Register<PlayerReadyStatusChanged>(OnPlayerReadyStatusChanged)
                .Register<NotifyPlayersChanged>(OnNotifyPlayersChanged)
                .Register<NotifyGameCharactersChanged>(OnNotifyGameCharactersChanged)
                ;

            _networkServerClient.OnError = OnNetworkClientError;
            _networkServerClient.OnConnected = OnNetworkClientConnected;
        }

        private void OnPlayerReadyStatusChanged(PlayerReadyStatusChanged readyStatusChanged)
        {
            lock (_actionlock)
            {
                var existingPlayer = GetPlayer(readyStatusChanged.PlayerId);
                if (existingPlayer == null)
                {
                    _logger.LogWarning("Can't find existing player. PlayerId={playerId}", readyStatusChanged.PlayerId);
                    return;
                }

                existingPlayer.IsReady = readyStatusChanged.IsReady;
                _lobbyWindowController.UpdatePlayers(_game.Players);
            }
        }

        private void OnNotifyGameCharactersChanged(NotifyGameCharactersChanged changed)
        {
            _logger.LogInformation("{messageType} received", nameof(NotifyGameCharactersChanged));
        }

        private void OnNotifyPlayersChanged(NotifyPlayersChanged changed)
        {
            _logger.LogInformation("{messageType} received", nameof(NotifyPlayersChanged));
            _game.Players.Clear();
            var players = changed.Players.Select(p => new NetworkPlayer(p.Id) { IsReady = p.IsReady, Name = p.Name }).ToList();
            _game.Players.AddRange(players);

            _lobbyWindowController.UpdatePlayers(_game.Players);
        }

        private void OnNetworkClientConnected(EndPoint endpoint)
        {
            _game = new NetworkGame(string.Empty);
            _lobbyWindowController.UpdateServerInfo(endpoint.ToString());
            OnConnected?.Invoke();
        }

        private void OnNetworkClientError(Exception exception)
        {
            if (exception is SocketException socketException)
            {
                string error = string.Empty;
                switch (socketException.SocketErrorCode)
                {
                    case SocketError.OperationAborted: // client disconnected by a user
                        _logger.LogWarning("Skipping notification. SocketCode={socketCode}", socketException.SocketErrorCode);
                        break;
                    case SocketError.ConnectionReset:
                        error = "You have been disconnected.";
                        break;
                    default:
                        error = $"Network error occurred. Error code: {socketException.SocketErrorCode}";
                        break;
                }

                if (!string.IsNullOrEmpty(error))
                {
                    OnNetworkError?.Invoke(error);
                }

                return;
            }

            // should never happen?
            OnNetworkError?.Invoke("Generic network error occurred.");
            _logger.LogError(exception, "Generic network error occurred");
        }

        private void OnPlayerNameRequest(PlayerNameRequest request)
        {
            _logger.LogInformation("Player name requested. PlayerId={playerId}", request.PlayerId);
            _localPlayerId = request.PlayerId;

            var nameResponse = new PlayerNameResponse() { Name = "mp client name" };
            _networkServerClient.SendAsync(nameResponse).Wait();
            _logger.LogInformation("Player name has been sent. Name={name}", nameResponse.Name);
        }

        public void Dispose()
        {
            _game?.Players.Clear();
            _game?.Portraits.Clear();
            _networkServerClient?.Dispose();
        }

        private NetworkPlayer GetPlayer(long playerId)
        {
            return _game.Players.FirstOrDefault(p => p.Id == playerId);
        }
    }
}
