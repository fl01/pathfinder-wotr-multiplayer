using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyCrusadeArmyCombatInitializationRequired)]
    public class NotifyCrusadeArmyCombatInitializationRequired
    {
        [ProtoMember(1)]
        public int Seed { get; set; }
    }
}
