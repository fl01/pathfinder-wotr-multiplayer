using System;
using System.Net;
using System.Threading.Tasks;

namespace WOTRMultiplayer.Networking.Abstractions
{
    public interface INetworkClient : INetworkReceiver
    {
        bool IsActive { get; }

        bool IsConnecting { get; }

        Task ConnectAsync(string host, int port);

        void Send(object message);

        T SendAndWaitFor<T>(object message)
            where T : class;

        Action<Exception> OnError { get; set; }

        Action<EndPoint> OnConnected { get; set; }

        void Reset();
    }
}
