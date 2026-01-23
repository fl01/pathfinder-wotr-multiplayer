using HarmonyLib;
using Kingmaker.Armies;
using Kingmaker.Blueprints.Root.Strings;
using Kingmaker.Kingdom.Armies;
using Kingmaker.UI;
using Kingmaker.UI.Common;
using Kingmaker.UI.MVVM._PCView.Crusade.ArmyInfo;
using Kingmaker.UI.MVVM._VM.Crusade.ArmyInfo;
using WOTRMultiplayer.Entities.GlobalMap;

namespace WOTRMultiplayer.HarmonyPatches.GlobalMap
{
    [HarmonyPatch]
    public class ArmyInfoArmyCartPatches
    {
        [HarmonyPatch(typeof(ArmyInfoArmyCartVM), nameof(ArmyInfoArmyCartVM.DismissAll))]
        [HarmonyPrefix]
        public static bool ArmyInfoArmyCartVM_DismissAll_Prefix(ArmyInfoArmyCartVM __instance)
        {
            if (!Main.Multiplayer.IsActive || __instance.State.Data.Faction != ArmyFaction.Crusaders)
            {
                return true;
            }

            var army = new NetworkGlobalMapArmy { Id = __instance.State.Id };
            Main.Multiplayer.OnGlobalMapCrusadeArmyDismiss(army);

            var popup = new NetworkGlobalMapCommonPopup { Type = NetworkGlobalMapCommonPopupType.DismissArmy };
            var message = string.Format(UIUtility.IsCrusadeEnabled ? UIStrings.Instance.CrusadeTexts.DismissArmyWarningWithReserve : UIStrings.Instance.CrusadeTexts.DismissArmyWarning, ArmyDismissManager.Instance.CalculateCompensation(__instance.State.Data));
            UIUtility.ShowMessageBox(message, MessageModalBase.ModalType.Dialog, delegate (MessageModalBase.ButtonType answer)
            {
                if (answer != MessageModalBase.ButtonType.Yes)
                {
                    Main.Multiplayer.OnGlobalMapCommonPopupDeclined(popup);
                    return;
                }

                Main.Multiplayer.OnGlobalMapCommonPopupAccepted(popup);
                ArmyDismissManager.Instance.DismissArmy(__instance.State.Data);
                __instance.OnClose();
            }, null, 0, null, null, null);
            Main.Multiplayer.OnGlobalMapCommonPopupShown(popup);
            return false;
        }

        [HarmonyPatch(typeof(ArmyInfoArmyCartView), nameof(ArmyInfoArmyCartView.SetArmyName))]
        [HarmonyPrefix]
        public static void ArmyInfoArmyCartView_SetArmyName_Prefix(ArmyInfoArmyCartView __instance, string armyName)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var globalMapArmy = new NetworkGlobalMapArmy { Id = __instance.ViewModel.State?.Id, Name = armyName };
            Main.Multiplayer.OnGlobalMapCrusadeArmyCartNameChanged(globalMapArmy);
        }

        [HarmonyPatch(typeof(ArmyInfoArmyCartPCView), nameof(ArmyInfoArmyCartPCView.BindViewImplementation))]
        [HarmonyPostfix]
        public static void ArmyInfoArmyCartPCView_BindViewImplementation_Postfix(ArmyInfoArmyCartPCView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var armyInfo = Main.UIAccessor.GlobalMapPCView?.m_ArmyInfoPCView;
            if (armyInfo?.m_MergeArmyCartView == __instance)
            {
                Main.Multiplayer.OnGlobalMapCrusadeArmyInfoMergeShown();
            }
        }

        [HarmonyPatch(typeof(ArmyInfoArmyCartVM), nameof(ArmyInfoArmyCartVM.OnClose))]
        [HarmonyPrefix]
        public static void ArmyInfoArmyCartVM_OnClose_Prefix(ArmyInfoArmyCartVM __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var armyInfo = Main.UIAccessor.GlobalMapPCView?.m_ArmyInfoPCView;
            if (armyInfo?.m_MainArmyCartView?.ViewModel == __instance)
            {
                Main.Multiplayer.OnGlobalMapCrusadeArmyMainCartClosed();
            }
            else if (armyInfo?.m_MergeArmyCartView?.ViewModel == __instance)
            {
                Main.Multiplayer.OnGlobalMapCrusadeArmyMergeCartClosed();
            }
            else if (Main.UIAccessor.GlobalMapPCView?.m_RecruitPCView?.m_ArmyView?.ViewModel == __instance)
            {
                Main.Multiplayer.OnGlobalMapCrusadeArmyRecruitCartClosed();
            }
        }
    }
}
