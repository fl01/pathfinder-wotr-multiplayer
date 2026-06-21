using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Networking.Abstractions.ExternalConnections;
using WOTRMultiplayer.Networking.Configuration;
using WOTRMultiplayer.Networking.ExternalConnectivity.Messages;

namespace WOTRMultiplayer.Networking.ExternalConnectivity
{
    public class ExternalConnectionService : IExternalConnectionService
    {
        private readonly ILogger<ExternalConnectionService> _logger;
        private readonly IExternalConnectionFactory _hubConnectionFactory;

        public Action<string> OnGameCodeChanged { get; set; }

        public Action OnConnected { get; set; }

        public Action OnError { get; set; }

        public Action OnReconnected { get; set; }

        public bool IsActive { get; private set; }

        private IExternalConnection _externalConnection;

        public ExternalConnectionService(
            ILogger<ExternalConnectionService> logger,
            IExternalConnectionFactory hubConnectionFactory)
        {
            _logger = logger;
            _hubConnectionFactory = hubConnectionFactory;
        }

        public async Task ConnectAsync(ExternalServerConfiguration externalServerConfiguration)
        {
            try
            {
                if (_externalConnection != null)
                {
                    await _externalConnection.StopAsync(default);
                }

                var baseUrl = new Uri(externalServerConfiguration.Server.Url);
                var fullUrl = new Uri(baseUrl, externalServerConfiguration.Server.GameHubPath);
                _externalConnection = _hubConnectionFactory.Create(fullUrl);
                _externalConnection
                    .On<GameCreatedMessage>(OnGameCreatedAsync)
                    .On<BeginConnectingMessage>(OnBeginConnectingAsync)
                    ;
                _externalConnection.OnReconnected = OnExternalConnectionReconnected;

                _logger.LogInformation("Connecting to external hub. Url={Url}", fullUrl);
                await _externalConnection.ConnectAsync(default);

                _logger.LogInformation("External connection has been estabilished. Url={Url}, AutoCreateGame={AutoCreateGame}", fullUrl, externalServerConfiguration.AutoCreateGame);
                IsActive = true;

                if (externalServerConfiguration.AutoCreateGame)
                {
                    var createGameMessage = new CreateGameMessage
                    {
                        Password = externalServerConfiguration.Password,
                        Port = externalServerConfiguration.Port
                    };

                    await _externalConnection.SendAsync(createGameMessage);
                }

                //var p2p = new P2PClient(_logger);
                //p2p.Start(9252);
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
        }

        public async Task JoinGameAsync(string code, string password, int port)
        {
            var joinGameMessage = new JoinGameMessage
            {
                Code = code,
                Password = password,
                Port = port
            };

            _logger.LogInformation("Joining game. Code={Code}, Port={Port}, HasPassword={HasPassword}", code, port, !string.IsNullOrEmpty(password));
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
            _logger.LogWarning("Received begin connecting. PeerId={PeerId}", beginConnectingMessage.PeerId);
            return Task.CompletedTask;
        }
    }
}
