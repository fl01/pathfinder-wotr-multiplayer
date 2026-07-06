using WOTRMultiplayer.Networking.Channels;

namespace WOTRMultiplayer.Networking.Consuming
{
    public class NetworkMessageMetadata
    {
        public long PlayerId { get; set; }

        public long ClientId { get; private set; }

        public object Message { get; private set; }

        public NetworkChannelType ChannelType { get; private set; }

        public NetworkMessageMetadata(NetworkChannelType networkChannelType, long clientId, object message)
        {
            ChannelType = networkChannelType;
            ClientId = clientId;
            Message = message;
        }
    }
}
