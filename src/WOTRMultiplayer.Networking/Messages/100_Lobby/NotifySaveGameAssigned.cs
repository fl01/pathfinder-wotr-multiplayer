using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Lobby
{
    [ProtoContract]
    [BeetleX.Packets.MessageType(106)]
    public class NotifySaveGameAssigned
    {
        [ProtoMember(1)]
        public byte[] Content { get; set; }
    }
}
