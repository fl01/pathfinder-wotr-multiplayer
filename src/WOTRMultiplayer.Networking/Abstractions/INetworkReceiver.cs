using System;

namespace WOTRMultiplayer.Networking.Abstractions
{
    public interface INetworkReceiver
    {
        INetworkReceiver On<TMessage>(Action<long, TMessage> messageHandler)
            where TMessage : class;
    }
}
