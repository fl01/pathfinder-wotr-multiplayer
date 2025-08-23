using System;
using System.Net;

namespace WOTRMultiplayer.Networking.Abstractions
{
    public interface INetworkServer : INetworkReceiver
    {
        bool IsActive { get; }

        Action<long> OnClientConnected { get; set; }

        Action<long> OnClientDisconnected { get; set; }

        Action<EndPoint> OnServerStarted { get; set; }

        void Send(long clientId, object message);

        T SendAndWaitFor<T>(long clientId, object message)
            where T : class;

        void SendAll(object message);

        void SendAllExcept(long clientId, object message);

        void Start(int hostPortRangeStart, int hostPortRangeEnd);

        void Reset();
    }
}
