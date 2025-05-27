using System;
using System.Net;
using WOTRMultiplayer.Networking.Messages.Lobby;

namespace WOTRMultiplayer.Networking.Abstractions
{
    public interface INetworkServer : IDisposable
    {
        bool IsActive { get; }
        Action<long> OnClientConnected { get; set; }
        Action<long> OnClientDisconnected { get; set; }
        Action<EndPoint> OnServerStarted { get; set; }

        INetworkServer Register<TMessage>(Action<long, TMessage> messageHandler)
            where TMessage : class;
        void Send(long playerId, object message);

        void Start();
    }
}
