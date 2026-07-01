using WOTRMultiplayer.Networking.Channels;

namespace WOTRMultiplayer.Networking.Consuming
{
    public class NetworkMessageMetadata
    {
        /// <summary>
        /// always 0 (default) for clients, as they are only connected to the host in both TCP server and P2P scenarios
        /// </summary>
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
