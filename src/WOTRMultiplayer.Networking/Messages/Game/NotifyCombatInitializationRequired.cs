using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyCombatInitializationRequired)]
    public class NotifyCombatInitializationRequired
    {
        [ProtoMember(1)]
        public NetworkCombatState State { get; set; }

        [ProtoMember(2)]
        public int Seed { get; set; }
    }
}
