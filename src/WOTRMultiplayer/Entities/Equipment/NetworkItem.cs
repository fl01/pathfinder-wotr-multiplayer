using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.Equipment
{
    public class NetworkItem
    {
        public string Id { get; set; }

        public string BlueprintId { get; set; }

        public string Name { get; set; }

        public int Count { get; set; }

        public int Cost { get; set; }

        public int EnchantmentValue { get; set; }

        public List<string> Enchantments { get; set; } = [];

        public string HoldingSlotOwnerId { get; set; }

        public string CollectionOwnerRef { get; set; }
    }
}
