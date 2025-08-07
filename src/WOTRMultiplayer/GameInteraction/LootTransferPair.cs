using Kingmaker.Items;
using WOTRMultiplayer.MP.Entities.Equipment;

namespace WOTRMultiplayer.GameInteraction
{
    public class LootTransferPair
    {
        public ItemEntity ItemEntity { get; set; }

        public NetworkItem NetworkItem { get; set; }
    }
}
