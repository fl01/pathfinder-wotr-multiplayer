using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType(1038)]
    public class ClientGameModeTypeEnded
    {
        [ProtoMember(1)]
        public int TypeId { get; set; }
    }
}
