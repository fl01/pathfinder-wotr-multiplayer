using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Lobby
{
    [ProtoContract]
    [BeetleX.Packets.MessageType(102)]
    public class PlayerReadyStatusChanged
    {
        [ProtoMember(1)]
        public bool IsReady { get; set; }
    }
}
