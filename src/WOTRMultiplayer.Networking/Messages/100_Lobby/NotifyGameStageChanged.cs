using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Lobby
{
    [ProtoContract]
    [BeetleX.Packets.MessageType(105)]
    public class NotifyGameStageChanged
    {
        [ProtoMember(1)]
        public string Stage { get; set; }
    }
}
