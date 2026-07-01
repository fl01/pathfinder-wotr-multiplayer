using System;
using System.Net;
using WOTRMultiplayer.Networking.Configuration;

namespace WOTRMultiplayer.Networking.Abstractions.TCP
{
    public interface INetworkServer : INetworkChannel
    {
        Action<long> OnClientConnected { get; set; }

        Action<long> OnClientDisconnected { get; set; }

        Action<EndPoint> OnServerStarted { get; set; }

        void Send(long clientId, object message);

        void BroadcastExcept(long clientId, object message);

        void Start(NetworkServerConfiguration networkServerConfiguration);

        void Reset();
    }
}
