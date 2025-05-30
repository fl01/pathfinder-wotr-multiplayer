using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.MP;
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

        private NetworkGame _game;
        private readonly object _actionlock = new();
        private long _localPlayerId;

        public Action<string> OnNetworkError { get; set; }

        public Action<EndPoint> OnConnected { get; set; }

        public Action<List<NetworkPlayer>> OnPlayersChanged { get; set; }
        public Action<List<string>> OnGameCharactersChanged { get; set; }

        public Action OnDisconnected { get; set; }

        public bool IsActive => _networkServerClient.IsActive;

        public bool IsConnecting => _networkServerClient.IsConnecting;

        private NetworkGameStatus Status => _game?.Status ?? NetworkGameStatus.None;

        public bool IsInLobby => IsActive && Status == NetworkGameStatus.Lobby;

        public MultiplayerClient(
            ILogger<MultiplayerClient> logger,
            IIPEndPointParser ipEndPointParser,
            INetworkServerClient networkServerClient)
        {
            _logger = logger;
            _ipEndPointParser = ipEndPointParser;
            _networkServerClient = networkServerClient;
        }

        public ConnectLobbyResult Connect(string address, MultiplayerSettings settings)
        {
            if (_networkServerClient.IsActive)
            {
                Dispose();
            }

            if (!_ipEndPointParser.TryParse(address, out IPEndPoint endpoint))
            {
                return ConnectLobbyResult.Error(StringConsts.MultiplayerClient.Errors.InvalidIP);
            }

            if (endpoint.Port <= 0 || endpoint.Port > ushort.MaxValue)
            {
                return ConnectLobbyResult.Error(StringConsts.MultiplayerClient.Errors.InvalidPort);
            }

            RegisterHandlers();

            _networkServerClient.ConnectAsync(endpoint.Address.ToString(), endpoint.Port);

            return ConnectLobbyResult.Ok();
        }

        public bool ReadyChanged()
        {
            _logger.LogInformation("Ready changed");
            var player = _game.Players.First(p => p.Id == _localPlayerId); // local client should be always present
            player.IsReady = !player.IsReady;
            var readyChanged = new PlayerReadyStatusChanged { PlayerId = player.Id, IsReady = player.IsReady };
            _networkServerClient.SendAsync(readyChanged).Wait();
            return readyChanged.IsReady;
        }

        public void Dispose()
        {
            _logger.LogInformation("Disposing");

            if (_game != null)
            {
                _game.Players.Clear();
                _game.Portraits.Clear();
                _game.Status = NetworkGameStatus.None;
            }

            _networkServerClient?.Dispose();
        }

        private void RegisterHandlers()
        {
            _networkServerClient
                .Register<PlayerNameRequest>(OnPlayerNameRequest)
                .Register<PlayerReadyStatusChanged>(OnPlayerReadyStatusChanged)
                .Register<NotifyPlayersChanged>(OnNotifyPlayersChanged)
                .Register<NotifyGameCharactersChanged>(OnNotifyGameCharactersChanged)
                .Register<NotifySaveGameAssigned>(OnNotifySaveGameAssigned)
                .Register<NotifyGameStatusChanged>(OnNotifyGameStatusChanged)
                ;

            _networkServerClient.OnError = OnNetworkClientError;
            _networkServerClient.OnConnected = OnNetworkClientConnected;
        }

        private void OnNotifyGameStatusChanged(NotifyGameStatusChanged changed)
        {
            _logger.LogInformation("Received NotifyGameStatusChanged. Status={newGameStatus}", changed.Status);
            _game.Status = (NetworkGameStatus)Enum.Parse(typeof(NetworkGameStatus), changed.Status, true);
        }

        private void OnNotifySaveGameAssigned(NotifySaveGameAssigned assigned)
        {
            _logger.LogInformation("Received save game file content. GameStatus={status} Size={contentSize}", _game.Status, assigned.Content.Length);
            // save content somewhere
            // send new is ready? (is ready to play)
        }

        private void OnPlayerReadyStatusChanged(PlayerReadyStatusChanged readyStatusChanged)
        {
            _logger.LogInformation("Player ready status changed received. PlayerId={playerId}, IsReady={isReady}", readyStatusChanged.PlayerId, readyStatusChanged.IsReady);

            lock (_actionlock)
            {
                var existingPlayer = GetPlayer(readyStatusChanged.PlayerId);
                if (existingPlayer == null)
                {
                    _logger.LogWarning("Can't find existing player. PlayerId={playerId}", readyStatusChanged.PlayerId);
                    return;
                }

                existingPlayer.IsReady = readyStatusChanged.IsReady;
                OnPlayersChanged?.Invoke(_game.Players);
            }
        }

        private void OnNotifyGameCharactersChanged(NotifyGameCharactersChanged changed)
        {
            _logger.LogInformation("{messageType} received", nameof(NotifyGameCharactersChanged));
            OnGameCharactersChanged?.Invoke(changed.Portraits);
        }

        private void OnNotifyPlayersChanged(NotifyPlayersChanged changed)
        {
            _logger.LogInformation("{messageType} received. PlayersCount={playersCount}}", nameof(NotifyPlayersChanged), changed.Players.Count);
            _game.Players.Clear();
            var players = changed.Players.Select(p => new NetworkPlayer(p.Id) { IsReady = p.IsReady, Name = p.Name }).ToList();
            _game.Players.AddRange(players);

            OnPlayersChanged?.Invoke(_game.Players);
        }

        private void OnNetworkClientConnected(EndPoint endpoint)
        {
            _game = new NetworkGame(string.Empty);
            OnConnected?.Invoke(endpoint);
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

        private NetworkPlayer GetPlayer(long playerId)
        {
            return _game.Players.FirstOrDefault(p => p.Id == playerId);
        }
    }
}
