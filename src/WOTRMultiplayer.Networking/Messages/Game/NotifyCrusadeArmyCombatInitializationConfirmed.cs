using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyCrusadeArmyCombatInitializationConfirmed)]
    public class NotifyCrusadeArmyCombatInitializationConfirmed
    {
        [ProtoMember(1)]
        public long PlayerId { get; set; }
    }
}
