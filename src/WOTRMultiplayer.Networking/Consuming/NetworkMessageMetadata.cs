using WOTRMultiplayer.Networking.Messages;

namespace WOTRMultiplayer.Networking.Consuming
{
    public class NetworkMessageMetadata
    {
        public long PlayerId { get; set; }

        public object Message { get; set; }

        public NetworkMessageMetadata(long playerId, object message)
        {
            PlayerId = playerId;
            Message = message;
        }
    }
}
