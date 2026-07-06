using System;
using System.Net;
using System.Threading.Tasks;
using WOTRMultiplayer.Networking.Awaiters;
using WOTRMultiplayer.Networking.Configuration;

namespace WOTRMultiplayer.Networking.Abstractions
{
    public interface INetworkClientConnection : INetworkConnection
    {
        bool IsConnecting { get; }

        Task<T> SendAndWaitForAsync<T>(IAwaitableRequest message)
            where T : IAwaitableResponse;

        Action<NetworkError> OnError { get; set; }

        Action<EndPoint, string> OnConnected { get; set; }

        Task ConnectAsync(string host, int port, TimeSpan awaiterTimeout);

        Task ConnectAsync(string code, string password, ExternalServerConfiguration externalServerConfiguration, TimeSpan awaiterTimeout);
    }
}
