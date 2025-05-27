using System;
using System.Threading.Tasks;

namespace WOTRMultiplayer.Networking.Abstractions
{
    public interface INetworkServerClient : IDisposable
    {
        Task ConnectAsync(string host, int port);

        INetworkServerClient Register<T>(Action<T> handler)
            where T : class;

        Task SendAsync(object message);
    }
}
