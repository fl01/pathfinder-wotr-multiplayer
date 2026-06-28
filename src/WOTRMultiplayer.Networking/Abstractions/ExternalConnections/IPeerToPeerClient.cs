using System;

namespace WOTRMultiplayer.Networking.Abstractions.ExternalConnections
{
    public interface IPeerToPeerClient
    {
        bool IsActive { get; }

        int LocalPort { get; }

        void Reset();

        bool Start(int port);

        void Introduce(string host, int port, string sessionId);

        void Send(object message);

        Action<int> OnNewPeerConnected { get; set; }
    }
}
