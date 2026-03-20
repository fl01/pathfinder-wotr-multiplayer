using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyCombatLocalTurnEnded)]
    public class NotifyCombatLocalTurnEnded : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public long PlayerId { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string UnitId { get; set; }
    }
}
