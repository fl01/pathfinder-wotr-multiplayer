using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages
{
    [ProtoContract]
    public class NetworkLootItem
    {
        [ProtoMember(1)]
        public string UniqueId { get; set; }

        [ProtoMember(2)]
        public string BlueprintId { get; set; }

        [ProtoMember(3)]
        public string Name { get; set; }

        [ProtoMember(4)]
        public int Count { get; set; }

        [ProtoMember(5)]
        public int Cost { get; set; }

        [ProtoMember(6)]
        public int EnchantmentValue { get; set; }

        [ProtoMember(7)]
        public string FirstEnchantmentName { get; set; }

        [ProtoMember(8)]
        public int EnchantmentsCount { get; set; }
    }
}
