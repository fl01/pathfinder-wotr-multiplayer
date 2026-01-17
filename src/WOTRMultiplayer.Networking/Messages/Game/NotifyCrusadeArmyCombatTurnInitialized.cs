using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyCrusadeArmyCombatTurnInitialized)]
    public class NotifyCrusadeArmyCombatTurnInitialized
    {
        [ProtoMember(1)]
        public long PlayerId { get; set; }

        [ProtoMember(2)]
        public int TurnNumber { get; set; }
    }
}
