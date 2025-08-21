using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyInvalidCombatTurnStarted)]
    public class NotifyInvalidCombatTurnStarted
    {
        [ProtoMember(1)]
        public string UnitId { get; set; }
    }
}
