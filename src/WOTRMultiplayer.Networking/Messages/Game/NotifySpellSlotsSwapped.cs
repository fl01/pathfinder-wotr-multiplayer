using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifySpellSlotsSwapped)]
    public class NotifySpellSlotsSwapped : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public string UnitId { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string SpellbookId { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public int SpellLevel { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public NetworkSpellSlot SlotA { get; set; }

        [ProtoMember(5)]
        [LogMe]
        public NetworkSpellSlot SlotB { get; set; }
    }
}
