using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Abstractions.ExternalConnections;
using WOTRMultiplayer.Networking.Configuration;
using WOTRMultiplayer.Networking.ExternalConnectivity.Messages;

namespace WOTRMultiplayer.Networking.ExternalConnectivity
{
    public class ExternalConnectionService : IExternalConnectionService
    {
        private readonly ILogger<ExternalConnectionService> _logger;
        private readonly IExternalConnectionFactory _externalConnectionFactory;

        private IPeerToPeerClient _peerToPeerClient;
        private IExternalConnection _externalConnection;
        private Uri _baseUrl;

        public Action<string> OnGameCodeChanged { get; set; }

        public Action OnConnected { get; set; }

        public Action OnError { get; set; }

        public Action OnReconnected { get; set; }

        public bool IsActive { get; private set; }

        public Action<int> OnNewExternalConnection { get; set; }

        public ExternalConnectionService(
            ILogger<ExternalConnectionService> logger,
            IExternalConnectionFactory externalConnectionFactory)
        {
            _logger = logger;
            _externalConnectionFactory = externalConnectionFactory;
        }

        public async Task ConnectAsync(ExternalServerConfiguration externalServerConfiguration, IMessageConsumer messageConsumer)
        {
            try
            {
                if (_externalConnection != null)
                {
                    await _externalConnection.StopAsync(default);
                }

                _baseUrl = new Uri(externalServerConfiguration.Server.Url);

                _logger.LogInformation("Connecting to external connection server. Url={Url}", _baseUrl);

                var fullUrl = new Uri(_baseUrl, externalServerConfiguration.Server.GameHubPath);
                _externalConnection = _externalConnectionFactory.Create(fullUrl);

                _externalConnection
                    .On<GameCreatedMessage>(OnGameCreatedAsync)
                    .On<BeginConnectingMessage>(OnBeginConnectingAsync)
                    ;
                _externalConnection.OnReconnected = OnExternalConnectionReconnected;

                await _externalConnection.ConnectAsync(default);
                _logger.LogInformation("External connection has been established. Url={Url}, AutoCreateGame={AutoCreateGame}", fullUrl, externalServerConfiguration.AutoCreateGame);

                _peerToPeerClient ??= _externalConnectionFactory.CreateP2P(messageConsumer);
                _peerToPeerClient.OnNewPeerConnected = OnNewPeerConnected;

                var isStarted = _peerToPeerClient.Start(externalServerConfiguration.Port);
                if (!isStarted)
                {
                    _logger.LogError("Unable to start p2p client. Port={Port}", externalServerConfiguration.Port);
                    return;
                }
                _logger.LogInformation("Peer-to-Peer client has been started. Port={Port}", _peerToPeerClient.LocalPort);
                IsActive = true;

                if (externalServerConfiguration.AutoCreateGame)
                {
                    var createGameMessage = new CreateGameMessage
                    {
                        Password = externalServerConfiguration.Password,
                        Port = _peerToPeerClient.LocalPort
                    };

                    await _externalConnection.SendAsync(createGameMessage);
                }

                OnConnected?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while connecting to hub");
                OnError?.Invoke();
            }
        }

        private void OnNewPeerConnected(int peerId)
        {
            _logger.LogInformation("New Peer. PeerId={PeerId}", peerId);
            OnNewExternalConnection?.Invoke(peerId);
        }

        public void Send(object message)
        {
            if (!IsPeerToPeerActive())
            {
                return;
            }

            _peerToPeerClient.Send(message);
        }

        public void Send(long clientId, object message)
        {
            if (!IsPeerToPeerActive())
            {
                return;
            }

            _peerToPeerClient.Send(message);
            _logger.LogError("TODO: Message should be sent to specific client");
        }

        public void SendAllExcept(long clientId, object message)
        {
            if (!IsPeerToPeerActive())
            {
                return;
            }

            _peerToPeerClient.Send(message);
            _logger.LogError("TODO: Message should be sent to specific clients");
        }

        public void Reset()
        {
            IsActive = false;
            _externalConnection?.StopAsync(default);
            _peerToPeerClient?.Reset();
            _peerToPeerClient = null;
        }

        public async Task JoinGameAsync(string code, string password)
        {
            if (!IsPeerToPeerActive())
            {
                _logger.LogWarning("Joining game is unavailable due to incorrect state of peer to peer client");
                return;
            }

            var joinGameMessage = new JoinGameMessage
            {
                Code = code,
                Password = password,
            };

            _logger.LogInformation("Joining game. Code={Code}, HasPassword={HasPassword}", joinGameMessage.Code, !string.IsNullOrEmpty(joinGameMessage.Password));
            await _externalConnection.SendAsync(joinGameMessage);
        }

        private bool IsPeerToPeerActive()
        {
            return _peerToPeerClient != null && _peerToPeerClient.IsActive;
        }

        private Task OnExternalConnectionReconnected()
        {
            return Task.CompletedTask;
        }

        private Task OnGameCreatedAsync(GameCreatedMessage gameCreatedMessage)
        {
            var gameCode = gameCreatedMessage.Game.Code;
            OnGameCodeChanged?.Invoke(gameCode);
            return Task.CompletedTask;
        }

        private Task OnBeginConnectingAsync(BeginConnectingMessage beginConnectingMessage)
        {
            _logger.LogInformation("Received begin connecting. SessionId={SessionId}, Port={Port}", beginConnectingMessage.SessionId, beginConnectingMessage.Port);
            _peerToPeerClient.Introduce(_baseUrl.Host, beginConnectingMessage.Port, beginConnectingMessage.SessionId);
            return Task.CompletedTask;
        }
    }
}
