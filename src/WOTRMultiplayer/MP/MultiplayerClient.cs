using System.Net;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Messages.Lobby;

namespace WOTRMultiplayer.MP
{
    public class MultiplayerClient : IMultiplayerClient
    {
        private readonly ILogger<MultiplayerClient> _logger;
        private readonly IIPEndPointParser _ipEndPointParser;
        private readonly INetworkServerClient _networkServerClient;

        public MultiplayerClient(
            ILogger<MultiplayerClient> logger,
            IIPEndPointParser ipEndPointParser,
            INetworkServerClient networkServerClient)
        {
            _logger = logger;
            _ipEndPointParser = ipEndPointParser;
            _networkServerClient = networkServerClient;

            RegisterHandlers();
        }

        public void Join(string address, MultiplayerSettings settings)
        {
            if (!_ipEndPointParser.TryParse(address, out IPEndPoint endpoint))
            {
                return;
            }


            _networkServerClient.ConnectAsync(endpoint.Address.ToString(), endpoint.Port).Wait();
        }

        private void RegisterHandlers()
        {
            _networkServerClient
                .Register<PlayerNameRequest>(OnPlayerNameRequest)
                ;
        }

        private void OnPlayerNameRequest(PlayerNameRequest request)
        {
            var nameResponse = new PlayerNameResponse() { Name = "AAA" };
            _networkServerClient.SendAsync(nameResponse);
        }
    }
}
