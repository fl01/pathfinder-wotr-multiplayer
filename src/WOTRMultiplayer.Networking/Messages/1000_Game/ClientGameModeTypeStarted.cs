using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType(1037)]
    public class ClientGameModeTypeStarted
    {
        [ProtoMember(1)]
        public int TypeId { get; set; }
    }
}
