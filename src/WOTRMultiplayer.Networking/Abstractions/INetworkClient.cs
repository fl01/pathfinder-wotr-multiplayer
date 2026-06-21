using System;
using System.Net;
using System.Threading.Tasks;
using WOTRMultiplayer.Networking.Awaiters;
using WOTRMultiplayer.Networking.Configuration;

namespace WOTRMultiplayer.Networking.Abstractions
{
    public interface INetworkClient : INetworkReceiver
    {
        bool IsConnecting { get; }

        Task ConnectAsync(string host, int port, TimeSpan awaiterTimeout);

        Task ConnectAsync(string code, string password, ExternalServerConfiguration externalServerConfiguration, TimeSpan awaiterTimeout);

        void Send(object message);

        Task<T> SendAndWaitForAsync<T>(IAwaitableRequest message)
            where T : IAwaitableResponse;

        Action<Exception> OnError { get; set; }

        Action<EndPoint> OnConnected { get; set; }

        void Reset();
    }
}
