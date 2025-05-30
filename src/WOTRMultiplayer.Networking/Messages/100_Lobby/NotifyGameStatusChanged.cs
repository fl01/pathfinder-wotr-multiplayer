using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Lobby
{
    [ProtoContract]
    [BeetleX.Packets.MessageType(105)]
    public class NotifyGameStatusChanged
    {
        [ProtoMember(1)]
        public string Status { get; set; }
    }
}
