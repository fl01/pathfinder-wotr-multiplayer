using System.Net;
using WOTRMultiplayer.Networking;

namespace WOTRMultiplayer
{
    public class MultiplayerClient
    {
        private readonly NetworkServerClient _networkServerClient;

        public MultiplayerClient(NetworkServerClient networkServerClient)
        {
            _networkServerClient = networkServerClient;
        }

        public void Join(string address, MultiplayerSettings settings)
        {
            if (!Networking.Extensions.IPEndPoint.TryParse(address, out IPEndPoint endpoint))
            {

                return;
            }

            _networkServerClient.Connect(endpoint.Address.ToString(), endpoint.Port);
        }
    }
}
