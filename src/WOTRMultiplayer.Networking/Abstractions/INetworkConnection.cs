using System;
using WOTRMultiplayer.Networking.Consuming;

namespace WOTRMultiplayer.Networking.Abstractions
{
    public interface INetworkConnection
    {
        void Broadcast(object message);

        void Reset();

        bool IsActive { get; }

        INetworkConnection On<TMessage>(Action<long, TMessage> messageHandler, MessageHandlerPriority priority = MessageHandlerPriority.Default)
            where TMessage : class;
    }
}
