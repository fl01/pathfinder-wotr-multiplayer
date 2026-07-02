using System;
using System.Net;
using WOTRMultiplayer.Networking.Configuration;

namespace WOTRMultiplayer.Networking.Abstractions
{
    public interface INetworkHostConnection : INetworkConnection
    {
        Action<long> OnPlayerConnected { get; set; }

        Action<long> OnPlayerDisconnected { get; set; }

        Action<EndPoint> OnLocalServerStarted { get; set; }

        Action<bool?, string> OnExternalConnectivityUpdated { get; set; }

        void EnableExternalConnections(ExternalServerConfiguration externalServerConfiguration);

        void HostTcpServer(NetworkServerConfiguration serverConfiguration);

        void BroadcastExcept(long playerId, object message);

        void Send(long playerId, object message);
    }
}
