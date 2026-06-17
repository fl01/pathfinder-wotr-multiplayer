using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Configuration;

namespace WOTRMultiplayer.Networking.ExternalConnectivity
{
    public class ExternalConnectionService : IExternalConnectionService
    {
        private readonly ILogger<ExternalConnectionService> _logger;
        private readonly IHubConnectionFactory _hubConnectionFactory;

        public Action<string> OnGameCodeChanged { get; set; }

        public Action OnConnected { get; set; }

        public Action OnError { get; set; }

        private HubConnection _hub;
        private readonly List<IDisposable> _connections = [];

        public ExternalConnectionService(
            ILogger<ExternalConnectionService> logger,
            IHubConnectionFactory hubConnectionFactory)
        {
            _logger = logger;
            _hubConnectionFactory = hubConnectionFactory;
        }

        public async Task ConnectAsync(ExternalServerConfiguration externalServer)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3));

                if (_hub != null)
                {
                    await _hub.StopAsync();
                }

                var baseUrl = new Uri(externalServer.Url);
                var fullUrl = new Uri(baseUrl, externalServer.GameHubPath);
                _hub = _hubConnectionFactory.Create(fullUrl);

                await _hub.StartAsync();
                _logger.LogInformation("Hub connection has been estabilished. Hub={Hub}", fullUrl);

                //var p2p = new P2PClient(_logger);
                //p2p.Start(9252);
                OnConnected?.Invoke();

                await Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(_ =>
                {
                    OnGameCodeChanged?.Invoke("EU1:TESTCODEABC");
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while connecting to hub");
                OnError?.Invoke();
            }
        }

        public void Reset()
        {
            _hub?.StopAsync();

            foreach (var connection in _connections)
            {
                try
                {
                    connection.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while disposing connection");
                }
            }

            _connections.Clear();
        }
    }
}
