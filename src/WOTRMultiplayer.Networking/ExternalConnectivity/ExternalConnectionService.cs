using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using WOTRMultiplayer.Networking.Abstractions.ExternalConnections;
using WOTRMultiplayer.Networking.Configuration;
using WOTRMultiplayer.Networking.ExternalConnectivity.Messages;

namespace WOTRMultiplayer.Networking.ExternalConnectivity
{
    public class ExternalConnectionService : IExternalConnectionService
    {
        private readonly ILogger<ExternalConnectionService> _logger;
        private readonly IExternalConnectionFactory _hubConnectionFactory;
        private readonly IPeerToPeerClient _peerToPeerClient;
        private IExternalConnection _externalConnection;
        private Uri _baseUrl;

        public Action<string> OnGameCodeChanged { get; set; }

        public Action OnConnected { get; set; }

        public Action OnError { get; set; }

        public Action OnReconnected { get; set; }

        public bool IsActive { get; private set; }


        public ExternalConnectionService(
            ILogger<ExternalConnectionService> logger,
            IExternalConnectionFactory hubConnectionFactory,
            IPeerToPeerClient peerToPeerClient)
        {
            _logger = logger;
            _hubConnectionFactory = hubConnectionFactory;
            _peerToPeerClient = peerToPeerClient;
        }

        public async Task ConnectAsync(ExternalServerConfiguration externalServerConfiguration)
        {
            try
            {
                if (_externalConnection != null)
                {
                    await _externalConnection.StopAsync(default);
                }

                _baseUrl = new Uri(externalServerConfiguration.Server.Url);

                var fullUrl = new Uri(_baseUrl, externalServerConfiguration.Server.GameHubPath);
                _externalConnection = _hubConnectionFactory.Create(fullUrl);
                _externalConnection
                    .On<GameCreatedMessage>(OnGameCreatedAsync)
                    .On<BeginConnectingMessage>(OnBeginConnectingAsync)
                    ;
                _externalConnection.OnReconnected = OnExternalConnectionReconnected;

                _logger.LogInformation("Connecting to external hub. Url={Url}", fullUrl);
                await _externalConnection.ConnectAsync(default);
                _logger.LogInformation("External connection has been estabilished. Url={Url}, AutoCreateGame={AutoCreateGame}", fullUrl, externalServerConfiguration.AutoCreateGame);

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

        public void Reset()
        {
            IsActive = false;
            _externalConnection?.StopAsync(default);
            _peerToPeerClient.Reset();
        }

        public async Task JoinGameAsync(string code, string password)
        {
            if (!_peerToPeerClient.IsActive)
            {
                _logger.LogWarning("Joining game is unavailable due to incorrect state of peer to peer client. IsActive={IsActive}", _peerToPeerClient.IsActive);
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
