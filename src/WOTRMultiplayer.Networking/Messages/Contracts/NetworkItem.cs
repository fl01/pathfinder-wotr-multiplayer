using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkItem
    {
        [ProtoMember(1)]
        [LogMe]
        public string UniqueId { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string BlueprintId { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public string Name { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public int Count { get; set; }

        [ProtoMember(5)]
        [LogMe]
        public int Cost { get; set; }

        [ProtoMember(6)]
        [LogMe]
        public int EnchantmentValue { get; set; }

        [ProtoMember(7)]
        public string FirstEnchantmentName { get; set; }

        [ProtoMember(8)]
        public int EnchantmentsCount { get; set; }

        [ProtoMember(9)]
        [LogMe]
        public string HoldingSlotOwnerId { get; set; }
    }
}
