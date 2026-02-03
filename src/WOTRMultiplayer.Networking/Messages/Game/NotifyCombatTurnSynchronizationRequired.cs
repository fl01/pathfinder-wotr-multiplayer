using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyCombatTurnSynchronizationRequired)]
    public class NotifyCombatTurnSynchronizationRequired
    {
        [ProtoMember(1)]
        public NetworkCombatState CombatState { get; set; }
    }
}
