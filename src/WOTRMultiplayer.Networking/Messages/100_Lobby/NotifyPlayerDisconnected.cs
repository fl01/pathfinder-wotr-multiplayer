using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Lobby
{
    [ProtoContract]
    [BeetleX.Packets.MessageType(110)]
    public class NotifyPlayerDisconnected
    {
        [ProtoMember(1)]
        public long PlayerId { get; set; }
    }
}
