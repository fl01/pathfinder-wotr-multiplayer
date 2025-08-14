using HarmonyLib;
using Kingmaker.Blueprints.Root.Strings;
using Kingmaker.UI;
using Kingmaker.UI.Common;
using Kingmaker.UI.MVVM._VM.Vendor;

namespace WOTRMultiplayer.HarmonyPatches.Vendor
{
    [HarmonyPatch]
    public class VendorVMPatches
    {
        [HarmonyPatch(typeof(VendorVM), nameof(VendorVM.Close))]
        [HarmonyPrefix]
        public static bool Vendor_Close_Prefix(VendorVM __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            CloseEx(__instance);
            return false;
        }

        // no desire to make this as a transpiler
        private static void CloseEx(VendorVM vendorVM)
        {
            if (vendorVM.IsItemInformationActive.Value)
            {
                vendorVM.SwitchItemInformationState();
            }
            else if (vendorVM.Vendor.IsChanged)
            {
                UIUtility.ShowMessageBox(UIStrings.Instance.Vendor.BeforeClose, MessageModalBase.ModalType.Dialog, delegate (MessageModalBase.ButtonType button)
                {
                    if (button == MessageModalBase.ButtonType.Yes)
                    {
                        Close(vendorVM);
                    }
                });
            }
            else
            {
                Close(vendorVM);
            }
        }

        private static void Close(VendorVM vendorVM)
        {
            if (vendorVM.m_CloseAction == null)
            {
                // not sure if this is even possible
                return;
            }

            Main.Multiplayer.OnCloseVendorWindow();
            vendorVM.m_CloseAction.Invoke();
        }
    }
}
