using System;
using System.Threading.Tasks;
using WOTRMultiplayer.Networking.Configuration;

namespace WOTRMultiplayer.Networking.Abstractions.ExternalConnections
{
    public interface IExternalConnectionService : INetworkChannel
    {
        Action<string> OnGameCodeChanged { get; set; }

        Action OnConnected { get; set; }

        Action<NetworkError> OnError { get; set; }

        Action<int, string> OnPeerConnected { get; set; }

        Action<int> OnPeerDisconnected { get; set; }

        bool IsConnecting { get; }

        Task ConnectAsync(ExternalServerConfiguration externalServerConfiguration);

        Task TerminateCoordinationAsync();

        void Reset();

        Task JoinGameAsync(string code, string password);

        void Send(long clientId, object message);

        void BroadcastExcept(long clientId, object message);
    }
}
