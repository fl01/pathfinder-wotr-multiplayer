using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkItem
    {
        [ProtoMember(1)]
        [LogMe]
        public string Id { get; set; }

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
        [LogMe]
        public List<string> Enchantments { get; set; } = [];

        [ProtoMember(8)]
        [LogMe]
        public string HoldingSlotOwnerId { get; set; }

        [ProtoMember(9)]
        [LogMe]
        public string CollectionOwnerRef { get; set; }

        public override string ToString()
        {
            return Id;
        }
    }
}
