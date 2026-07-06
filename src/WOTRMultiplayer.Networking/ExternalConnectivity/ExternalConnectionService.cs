using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Networking.Abstractions.ExternalConnections;
using WOTRMultiplayer.Networking.Configuration;
using WOTRMultiplayer.Networking.Consuming;
using WOTRMultiplayer.Networking.ExternalConnectivity.Messages;

namespace WOTRMultiplayer.Networking.ExternalConnectivity
{
    public class ExternalConnectionService : IExternalConnectionService
    {
        private readonly ILogger<ExternalConnectionService> _logger;
        private readonly IPeerToPeerFactory _peerToPeerFactory;
        private readonly IPeerToPeerClient _peerToPeerClient;

        private IPeerToPeerCoordinator _peerToPeerCoordinator;
        private Uri _baseUrl;
        private string _latestGameCode;

        public Action<string> OnGameCodeChanged { get; set; }

        public Action OnConnected { get; set; }

        public Action<NetworkError> OnError { get; set; }

        public Action OnReconnected { get; set; }

        public bool IsActive { get; private set; }

        public bool IsConnecting { get; private set; }

        public Action<int, string> OnPeerConnected { get; set; }

        public Action<int> OnPeerDisconnected { get; set; }

        public Action<NetworkMessageMetadata> OnMessageReceived { get; set; }

        public ExternalConnectionService(
            ILogger<ExternalConnectionService> logger,
            IPeerToPeerFactory peerToPeerFactory,
            IPeerToPeerClient peerToPeerClient)
        {
            _logger = logger;
            _peerToPeerFactory = peerToPeerFactory;
            _peerToPeerClient = peerToPeerClient;
        }

        public async Task ConnectAsync(ExternalServerConfiguration externalServerConfiguration)
        {
            try
            {
                IsConnecting = true;
                _baseUrl = new Uri(externalServerConfiguration.Server.Url);

                _logger.LogInformation("Connecting to P2P coordinator. Url={Url}", _baseUrl);

                var fullUrl = new Uri(_baseUrl, externalServerConfiguration.Server.GameHubPath);
                _peerToPeerCoordinator = _peerToPeerFactory.Create(fullUrl);

                _peerToPeerCoordinator
                    .On<GameNotFoundMessage>(OnGameNotFoundAsync)
                    .On<GameHostUnavailableMessage>(OnGameHostUnavailableAsync)
                    .On<GameCreatedMessage>(OnGameCreatedAsync)
                    .On<BeginConnectingMessage>(OnBeginConnectingAsync)
                    ;

                _peerToPeerCoordinator.OnReconnected = () => AutoCreateGameAsync(externalServerConfiguration);
                _peerToPeerCoordinator.OnReconnecting = () => OnReconnectingCoordinator();

                await _peerToPeerCoordinator.ConnectAsync();
                _logger.LogInformation("Connection to P2P coordinator has been established. Url={Url}, AutoCreateGame={AutoCreateGame}", fullUrl, externalServerConfiguration.AutoCreateGame);

                _peerToPeerClient.OnPeerConnectedEvent = OnPeerConnectedEvent;
                _peerToPeerClient.OnPeerDisconnectedEvent = OnPeerDisconnectedEvent;
                _peerToPeerClient.OnMessageReceived = OnP2PMessageReceived;

                var isStarted = _peerToPeerClient.Start(externalServerConfiguration.Port);
                if (!isStarted)
                {
                    _logger.LogError("Unable to start P2P client. Port={Port}", externalServerConfiguration.Port);
                    return;
                }

                _logger.LogInformation("P2P client has been started. Port={Port}", _peerToPeerClient.LocalPort);
                IsActive = true;
                await AutoCreateGameAsync(externalServerConfiguration);

                OnConnected?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while connecting to P2P coordinator");
                var unableToConnectError = new NetworkError(NetworkErrorType.UnreachableSignalingServer);
                OnError?.Invoke(unableToConnectError);
                IsConnecting = false;
            }
        }

        private async Task OnReconnectingCoordinator()
        {
            _latestGameCode = null;
            OnGameCodeChanged.Invoke(null);
        }

        private void OnP2PMessageReceived(NetworkMessageMetadata networkMessageMetadata)
        {
            OnMessageReceived?.Invoke(networkMessageMetadata);
        }

        private void OnPeerConnectedEvent(int peerId)
        {
            OnPeerConnected?.Invoke(peerId, _latestGameCode);
        }

        private void OnPeerDisconnectedEvent(int peerId)
        {
            OnPeerDisconnected?.Invoke(peerId);
        }

        public void Broadcast(object message)
        {
            _peerToPeerClient.Broadcast(message);
        }

        public void Send(long clientId, object message)
        {
            _peerToPeerClient.Send(clientId, message);
        }

        public void BroadcastExcept(long clientId, object message)
        {
            _peerToPeerClient.BroadcastExcept(clientId, message);
        }

        public void Reset()
        {
            _logger.LogInformation("Reset");
            IsActive = false;
            _peerToPeerCoordinator?.StopAsync(_latestGameCode);
            _peerToPeerClient.Reset();
        }

        public async Task JoinGameAsync(string code, string password)
        {
            if (!IsPeerToPeerActive())
            {
                _logger.LogWarning("Joining game is unavailable due to incorrect state of P2P client");
                return;
            }

            var joinGameMessage = new JoinGameMessage
            {
                Code = code,
                Password = password,
            };

            _logger.LogInformation("Joining game. Code={Code}, HasPassword={HasPassword}", joinGameMessage.Code, !string.IsNullOrEmpty(joinGameMessage.Password));
            await _peerToPeerCoordinator.SendAsync(joinGameMessage);
        }

        private bool IsPeerToPeerActive()
        {
            return _peerToPeerClient != null && _peerToPeerClient.IsActive;
        }

        private async Task AutoCreateGameAsync(ExternalServerConfiguration externalServerConfiguration)
        {
            if (!externalServerConfiguration.AutoCreateGame)
            {
                return;
            }

            var createGameMessage = new CreateGameMessage
            {
                Password = externalServerConfiguration.Password,
                Port = _peerToPeerClient.LocalPort
            };

            await _peerToPeerCoordinator.SendAsync(createGameMessage);
        }

        private Task OnGameHostUnavailableAsync(GameHostUnavailableMessage message)
        {
            IsConnecting = false;
            var error = new NetworkError(NetworkErrorType.GameHostUnavailable);
            OnError?.Invoke(error);
            return Task.CompletedTask;
        }

        private Task OnGameNotFoundAsync(GameNotFoundMessage message)
        {
            IsConnecting = false;
            var error = new NetworkError(NetworkErrorType.GameNotFound);
            OnError?.Invoke(error);
            return Task.CompletedTask;
        }

        private Task OnGameCreatedAsync(GameCreatedMessage message)
        {
            _latestGameCode = message.Game.Code;
            OnGameCodeChanged?.Invoke(_latestGameCode);
            return Task.CompletedTask;
        }

        private Task OnBeginConnectingAsync(BeginConnectingMessage message)
        {
            // this is applicable only to the client, since the host creates the game and sets the game code when connecting to the coordinator
            if (string.IsNullOrEmpty(_latestGameCode))
            {
                _latestGameCode = message.GameCode;
            }

            _peerToPeerClient.Introduce(_baseUrl.Host, message.Port, message.SessionId);
            return Task.CompletedTask;
        }
    }
}
