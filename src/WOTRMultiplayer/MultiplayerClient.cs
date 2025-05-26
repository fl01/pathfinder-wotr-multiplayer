using System.Net;
using WOTRMultiplayer.Networking;
using WOTRMultiplayer.Networking.Messages.Lobby;

namespace WOTRMultiplayer
{
    public class MultiplayerClient
    {
        private readonly NetworkServerClient _networkServerClient;

        public MultiplayerClient(NetworkServerClient networkServerClient)
        {
            _networkServerClient = networkServerClient;

            RegisterHandlers();
        }

        public void Join(string address, MultiplayerSettings settings)
        {
            if (!Networking.Extensions.IPEndPoint.TryParse(address, out IPEndPoint endpoint))
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
            _networkServerClient.Send(nameResponse);
        }
    }
}
