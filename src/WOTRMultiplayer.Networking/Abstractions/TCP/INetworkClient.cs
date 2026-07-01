using System;
using System.Net;
using System.Threading.Tasks;

namespace WOTRMultiplayer.Networking.Abstractions.TCP
{
    public interface INetworkClient : INetworkChannel
    {
        bool IsConnecting { get; }

        Task ConnectAsync(string host, int port);

        Action<Exception> OnError { get; set; }

        Action<EndPoint> OnConnected { get; set; }

        void Reset();
    }
}
