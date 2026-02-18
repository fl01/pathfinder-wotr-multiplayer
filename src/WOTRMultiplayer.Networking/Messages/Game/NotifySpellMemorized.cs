using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifySpellMemorized)]
    public class NotifySpellMemorized
    {
        [ProtoMember(1)]
        [LogMe]
        public string UnitId { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public NetworkSpellSlot Slot { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public NetworkAbility Ability { get; set; }
    }
}
