using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Lobby
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Lobby.NotifyGameStageChanged)]
    public class NotifyGameStageChanged
    {
        [ProtoMember(1)]
        public string Stage { get; set; }
    }
}
