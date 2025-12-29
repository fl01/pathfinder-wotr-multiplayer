using WOTRMultiplayer.Entities.Equipment;

namespace WOTRMultiplayer.Entities.Vendor
{
    public class NetworkVendorItemTransfer
    {
        public NetworkItem Item { get; set; }

        public int Count { get; set; }

        public VendorItemAction ItemAction { get; set; }

        public VendorItemActionTarget ItemActionTarget { get; set; }
    }
}
