using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType(1041)]
    public class NotifyInvalidCombatTurnStarted
    {
        [ProtoMember(1)]
        public string UnitId { get; set; }
    }
}
