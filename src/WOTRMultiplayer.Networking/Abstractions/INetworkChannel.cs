using System;
using WOTRMultiplayer.Networking.Consuming;

namespace WOTRMultiplayer.Networking.Abstractions
{
    public interface INetworkChannel
    {
        bool IsActive { get; }

        Action<NetworkMessageMetadata> OnMessageReceived { get; set; }

        void Broadcast(object message);
    }
}
