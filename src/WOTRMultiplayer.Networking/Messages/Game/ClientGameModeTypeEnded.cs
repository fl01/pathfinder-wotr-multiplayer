using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.ClientGameModeTypeEnded)]
    public class ClientGameModeTypeEnded
    {
        [ProtoMember(1)]
        public int TypeId { get; set; }
    }
}
