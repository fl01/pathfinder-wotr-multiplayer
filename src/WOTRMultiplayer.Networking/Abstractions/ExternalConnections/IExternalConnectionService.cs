using System;
using System.Threading.Tasks;
using WOTRMultiplayer.Networking.Configuration;

namespace WOTRMultiplayer.Networking.Abstractions.ExternalConnections
{
    public interface IExternalConnectionService : INetworkChannel
    {
        Action<string> OnGameCodeChanged { get; set; }

        Action OnConnected { get; set; }

        Action OnError { get; set; }

        Action<int> OnPeerConnected { get; set; }

        Action<int> OnPeerDisconnected { get; set; }

        Task ConnectAsync(ExternalServerConfiguration externalServerConfiguration);

        void Reset();

        Task JoinGameAsync(string code, string password);

        void Send(long clientId, object message);

        void BroadcastExcept(long clientId, object message);
    }
}
