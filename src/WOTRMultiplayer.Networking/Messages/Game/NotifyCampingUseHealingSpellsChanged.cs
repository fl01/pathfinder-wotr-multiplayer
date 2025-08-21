using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyCampingUseHealingSpellsChanged)]
    public class NotifyCampingUseHealingSpellsChanged
    {
        [ProtoMember(1)]
        public bool IsOn { get; set; }
    }
}
