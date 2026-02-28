using System;
using WOTRMultiplayer.Networking.Consuming;

namespace WOTRMultiplayer.Networking.Abstractions
{
    public interface IMessageConsumer
    {
        void On<TMessage>(Action<long, TMessage> messageHandler, MessageHandlerPriority priority)
            where TMessage : class;

        void Enqueue(NetworkMessageMetadata message);

        void Reset();
    }
}
