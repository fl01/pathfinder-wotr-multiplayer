using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType(1022)]
    public class ClientCombatTurnSynchronized
    {
        [ProtoMember(1)]
        public string UnitId { get; set; }
    }
}
