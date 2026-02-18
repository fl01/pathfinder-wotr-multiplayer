using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyCombatPreparationRequired)]
    public class NotifyCombatPreparationRequired
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkCombatUnitDiscrepancy Discrepancy { get; set; }
    }
}
