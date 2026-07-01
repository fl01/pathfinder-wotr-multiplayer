using System;

namespace WOTRMultiplayer.Networking.Abstractions.ExternalConnections
{
    public interface IPeerToPeerClient : INetworkChannel
    {
        int LocalPort { get; }

        void Reset();

        bool Start(int port);

        void Introduce(string host, int port, string sessionId);

        void Send(long clientId, object message);

        void BroadcastExcept(long clientId, object message);

        Action<int> OnPeerConnectedEvent { get; set; }

        Action<int> OnPeerDisconnectedEvent { get; set; }
    }
}
