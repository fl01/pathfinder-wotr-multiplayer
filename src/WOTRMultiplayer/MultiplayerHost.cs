using WOTRMultiplayer.Networking;

namespace WOTRMultiplayer
{
    public class MultiplayerHost
    {
        private readonly NetworkServer _networkServer;

        public bool IsActive => _networkServer.IsActive;

        public MultiplayerHost(NetworkServer networkServer)
        {
            _networkServer = networkServer;
        }

        public void Create(MultiplayerSettings settings)
        {
            _networkServer.Start(settings.NetworkInterfaceBinding, settings.Port);
        }
    }
}
