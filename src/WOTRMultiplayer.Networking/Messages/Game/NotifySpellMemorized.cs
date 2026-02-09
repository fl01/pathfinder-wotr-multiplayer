using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifySpellMemorized)]
    public class NotifySpellMemorized
    {
        [ProtoMember(1)]
        public string UnitId { get; set; }

        [ProtoMember(2)]
        public NetworkSpellSlot Slot { get; set; }

        [ProtoMember(3)]
        public NetworkAbility Ability { get; set; }
    }
}
