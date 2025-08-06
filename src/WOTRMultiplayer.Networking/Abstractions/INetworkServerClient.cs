using System;
using System.Net;
using System.Threading.Tasks;

namespace WOTRMultiplayer.Networking.Abstractions
{
    public interface INetworkServerClient : IDisposable
    {
        bool IsActive { get; }

        bool IsConnecting { get; }

        Task ConnectAsync(string host, int port);

        INetworkServerClient Register<T>(Action<T> handler)
            where T : class;

        void Send(object message);

        Task SendAsync(object message);

        Task<T> SendAndWaitForAsync<T>(object message)
            where T : class;

        Action<Exception> OnError { get; set; }

        Action<EndPoint> OnConnected { get; set; }
    }
}
