using HarmonyLib;
using Kingmaker.Items;
using WOTRMultiplayer.Entities.Equipment;
using WOTRMultiplayer.Entities.Vendor;

namespace WOTRMultiplayer.HarmonyPatches.Vendor
{
    [HarmonyPatch]
    public class VendorLogicPatches
    {
        [HarmonyPatch(typeof(VendorLogic), nameof(VendorLogic.Deal))]
        [HarmonyPrefix]
        public static void VendorLogic_Deal_Prefix(VendorLogic __instance)
        {
            if (!Main.Multiplayer.IsActive || !__instance.IsDealPossible)
            {
                return;
            }

            Main.Multiplayer.OnMakeVendorDeal();
        }

        [HarmonyPatch(typeof(VendorLogic), nameof(VendorLogic.AddForBuy))]
        [HarmonyPostfix]
        public static void VendorLogic_AddForBuy_Postfix(ItemEntity item, int count, ref ItemEntity __result)
        {
            if (!Main.Multiplayer.IsActive || __result == null)
            {
                return;
            }

            var transfer = CreateItemTransfer(item, count, VendorItemAction.Add, VendorItemActionTarget.Buy);
            Main.Multiplayer.OnTransferVendorItem(transfer);
        }

        [HarmonyPatch(typeof(VendorLogic), nameof(VendorLogic.RemoveFromBuy))]
        [HarmonyPostfix]
        public static void VendorLogic_RemoveFromBuy_Postfix(ItemEntity item, int count, ref ItemEntity __result)
        {
            if (!Main.Multiplayer.IsActive || __result == null)
            {
                return;
            }

            var transfer = CreateItemTransfer(item, count, VendorItemAction.Remove, VendorItemActionTarget.Buy);
            Main.Multiplayer.OnTransferVendorItem(transfer);
        }

        [HarmonyPatch(typeof(VendorLogic), nameof(VendorLogic.AddForSell))]
        [HarmonyPrefix]
        public static void VendorLogic_AddForSell_Prefix(ItemEntity item, int count)
        {
            if (!Main.Multiplayer.IsActive || item.Collection != VendorLogic.Player.Inventory)
            {
                return;
            }

            // bulk sell: -1 means ALL items
            var actualCount = count == -1 ? item.Count : count;
            var transfer = CreateItemTransfer(item, actualCount, VendorItemAction.Add, VendorItemActionTarget.Sell);
            Main.Multiplayer.OnTransferVendorItem(transfer);
        }

        [HarmonyPatch(typeof(VendorLogic), nameof(VendorLogic.RemoveFromSell))]
        [HarmonyPostfix]
        public static void VendorLogic_RemoveFromSell_Postfix(ItemEntity item, int count, ref ItemEntity __result)
        {
            if (!Main.Multiplayer.IsActive || __result == null)
            {
                return;
            }

            var transfer = CreateItemTransfer(item, count, VendorItemAction.Remove, VendorItemActionTarget.Sell);
            Main.Multiplayer.OnTransferVendorItem(transfer);
        }

        private static NetworkVendorItemTransfer CreateItemTransfer(ItemEntity item, int count, VendorItemAction itemAction, VendorItemActionTarget actionTarget)
        {
            return new NetworkVendorItemTransfer
            {
                Item = Main.Mapper.Map<NetworkItem>(item),
                Count = count,
                ItemAction = itemAction,
                ItemActionTarget = actionTarget
            };
        }
    }
}
