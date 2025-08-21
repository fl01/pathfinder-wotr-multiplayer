using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Lobby
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Lobby.NotifyPlayerDisconnected)]
    public class NotifyPlayerDisconnected
    {
        [ProtoMember(1)]
        public long PlayerId { get; set; }
    }
}
