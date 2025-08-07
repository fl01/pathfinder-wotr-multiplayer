using System.Linq;
using Kingmaker.Items;

namespace WOTRMultiplayer.MP.Entities.Equipment
{
    public class NetworkItem
    {
        public string UniqueId { get; set; }

        public string BlueprintId { get; set; }

        public string Name { get; set; }

        public int Count { get; set; }

        public int Cost { get; set; }

        public int EnchantmentValue { get; set; }

        public string FirstEnchantmentName { get; set; }

        public int EnchantmentsCount { get; set; }

        public static NetworkItem FromItemEntity(ItemEntity itemEntity)
        {
            var item = new NetworkItem
            {
                UniqueId = itemEntity.UniqueId,
                BlueprintId = itemEntity.Blueprint.AssetGuid.ToString(),
                Name = itemEntity.NameForAcronym,
                Count = itemEntity.Count,
                Cost = itemEntity.Cost,
                EnchantmentValue = itemEntity.EnchantmentValue,
                EnchantmentsCount = itemEntity.Enchantments.Count,
                FirstEnchantmentName = itemEntity.Enchantments.FirstOrDefault()?.NameForAcronym,
            };

            return item;
        }
    }
}
