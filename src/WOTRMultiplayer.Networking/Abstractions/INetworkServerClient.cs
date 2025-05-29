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

        Task SendAsync(object message);

        Action<Exception> OnError { get; set; }
        Action<EndPoint> OnConnected { get; set; }
        Action OnDisconnected { get; set; }
    }
}
