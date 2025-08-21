using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.ClientCharacterLevelingRequested)]
    public class ClientCharacterLevelingRequested
    {
        [ProtoMember(1)]
        public string UnitId { get; set; }
    }
}
