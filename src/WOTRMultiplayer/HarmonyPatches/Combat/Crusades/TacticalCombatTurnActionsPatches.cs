using HarmonyLib;
using Kingmaker;
using Kingmaker.Armies.TacticalCombat;
using Kingmaker.Armies.TacticalCombat.Controllers;
using Kingmaker.Localization;
using Kingmaker.PubSubSystem;
using Kingmaker.UI.MVVM._PCView.TacticalCombat;
using Kingmaker.UI.MVVM._PCView.TacticalCombat.ActionBar;
using Kingmaker.UI.MVVM._VM.TacticalCombat.ActionBar;

namespace WOTRMultiplayer.HarmonyPatches.Combat.Crusades
{
    [HarmonyPatch]
    public class TacticalCombatTurnActionsPatches
    {
        [HarmonyPatch(typeof(TacticalCombatPCView), nameof(TacticalCombatPCView.BindViewImplementation))]
        [HarmonyPostfix]
        public static void TacticalCombatPCView_BindViewImplementation_Postfix(TacticalCombatPCView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            __instance.m_AutoCombatButton.Interactable = false;
            __instance.m_FleeButton.Interactable = Main.Multiplayer.CanControlTacticalCombat();
        }

        [HarmonyPatch(typeof(TacticalCombatController), nameof(TacticalCombatController.RetreatFromBattle))]
        [HarmonyPrefix]
        public static void TacticalCombatController_RetreatFromBattle_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnTacticalCombatRetreat();
        }

        [HarmonyPatch(typeof(TacticalCombatUnitActionBarBaseView), nameof(TacticalCombatUnitActionBarBaseView.UpdateHoldButtonState))]
        [HarmonyPostfix]
        public static void TacticalCombatTurnController_UpdateHoldButtonState_Postfix(TacticalCombatUnitActionBarBaseView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var canContinue = Main.Multiplayer.CanControlTacticalCombat();
            if (!canContinue && __instance.m_HoldButton.Interactable)
            {
                __instance.m_HoldButton.Interactable = false;
            }

            if (!canContinue && __instance.m_DefenseButton.Interactable)
            {
                __instance.m_DefenseButton.Interactable = false;
            }
        }

        /// <summary>
        /// Still can be enabled via CheatsTacticalCombat, might be useful if you are in 1-player multiplayer session
        /// </summary>
        /// <returns></returns>
        [HarmonyPatch(typeof(TacticalCombatController), nameof(TacticalCombatController.ToggleAutoCombat))]
        [HarmonyPrefix]
        public static bool TacticalCombatController_ToggleAutoCombat_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var message = new LocalizedString { Key = WellKnownKeys.GameNotifications.TacticalCombat.DisabledAutoCombat.Key };
            EventBus.RaiseEvent<IWarningNotificationUIHandler>(x => x.HandleWarning(message, false));
            return false;
        }

        [HarmonyPatch(typeof(TacticalCombatController), nameof(TacticalCombatController.HandleTotalDefensePressed))]
        [HarmonyPrefix]
        public static bool TacticalCombatTurnController_HandleTotalDefensePressed_Prefix(TacticalCombatController __instance)
        {
            var turn = Game.Instance.TacticalCombat.Data.Turn;
            var unit = turn.Unit;

            if (!Main.Multiplayer.IsActive || unit == null || __instance.TurnController.TurnEnded || turn.StandardActionUsed)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.OnTacticalCombatTotalDefenseUsed();
            return canContinue;
        }

        [HarmonyPatch(typeof(TacticalCombatController), nameof(TacticalCombatController.HandlePostponeTurnPressed))]
        [HarmonyPrefix]
        public static bool TacticalCombatTurnController_HandlePostponeTurnPressed_Prefix(TacticalCombatController __instance)
        {
            var turn = Game.Instance.TacticalCombat.Data.Turn;
            if (!Main.Multiplayer.IsActive || __instance.TurnController.TurnEnded || turn.StandardActionUsed)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.OnTacticalCombatTurnPostponed();
            return canContinue;
        }

        [HarmonyPatch(typeof(UnitCrusadeActionBarVM), nameof(UnitCrusadeActionBarVM.OnDefense))]
        [HarmonyPrefix]
        public static bool UnitCrusadeActionBarVM_OnDefense_Prefix()
        {
            if (!Main.Multiplayer.IsActive || !TacticalCombatHelper.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.OnTacticalCombatTotalDefenseUsed();
            return canContinue;
        }

        [HarmonyPatch(typeof(UnitCrusadeActionBarVM), nameof(UnitCrusadeActionBarVM.OnHold))]
        [HarmonyPrefix]
        public static bool UnitCrusadeActionBarVM_OnHold_Prefix()
        {
            if (!Main.Multiplayer.IsActive || !TacticalCombatHelper.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.OnTacticalCombatTurnPostponed();
            return canContinue;
        }

        [HarmonyPatch(typeof(ActionBarSlotVM), nameof(ActionBarSlotVM.OnClick))]
        [HarmonyPrefix]
        public static bool ActionBarSlotVM_OnClick_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.CanControlTacticalCombat();
            return canContinue;
        }
    }
}
