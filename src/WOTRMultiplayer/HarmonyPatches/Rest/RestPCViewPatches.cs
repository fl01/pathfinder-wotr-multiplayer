using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.Rest;

namespace WOTRMultiplayer.HarmonyPatches.Rest
{
    [HarmonyPatch]
    public class RestPCViewPatches
    {
        /// <summary>
        /// 'Use Spells' toggle is a field sadly, so this patch is required
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPatch(typeof(RestPCView), nameof(RestPCView.SetHealingState))]
        [HarmonyPrefix]
        public static bool RestPCView_SetHealingState_Prefix(RestPCView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var shouldContinue = Main.Multiplayer.OnCampingUseHealingSpellsChanged(__instance.m_HealingToggle.isOn);
            return shouldContinue;
        }

        [HarmonyPatch(typeof(RestPCView), nameof(RestPCView.StartRest))]
        [HarmonyPrefix]
        public static void RestPCView_StartRest_Prefix(RestPCView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnStartRest();
        }

        [HarmonyPatch(typeof(RestPCView), nameof(RestPCView.BindViewImplementation))]
        [HarmonyPostfix]
        public static void RestPCView_BindViewImplementation_Postfix(RestPCView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var canUseRestUI = Main.Multiplayer.CanUseCampingUI();
            if (canUseRestUI)
            {
                return;
            }

            DisableRestUI(__instance);
        }

        private static void DisableRestUI(RestPCView view)
        {
            view.m_StartRestButton.Interactable = false;
            view.m_HealingToggle.interactable = false;
            view.m_AutotuneToggle.interactable = false;
            view.m_AutoGroupButton.Interactable = false;

            view.m_DivineServiceRoles.m_Button.Interactable = false;
            DisablePortraits(view.m_DivineServiceRoles.m_FirstPortraitsView);

            view.m_CamouflageRoles.m_Button.Interactable = false;
            DisablePortraits(view.m_CamouflageRoles.m_FirstPortraitsView);

            view.m_GuardRestRoles.m_Button.Interactable = false;
            DisablePortraits(view.m_GuardRestRoles.m_FirstPortraitsView);
            DisablePortraits(view.m_GuardRestRoles.m_SecondPortraitsView);

            view.m_AlchemyRoles.m_Button.Interactable = false;
            view.m_AlchemyRoles.m_BrothIconButton.Interactable = false;
            view.m_AlchemyRoles.m_BrothIconButton.OnLeftClick.RemoveAllListeners();
            view.m_AlchemyRoles.m_BrothIconButton.OnLeftClickNotInteractable.RemoveAllListeners();
            view.m_AlchemyRoles.m_PotionIconButton.Interactable = false;
            view.m_AlchemyRoles.m_PotionIconButton.OnLeftClick.RemoveAllListeners();
            view.m_AlchemyRoles.m_PotionIconButton.OnLeftClickNotInteractable.RemoveAllListeners();
            DisablePortraits(view.m_AlchemyRoles.m_FirstPortraitsView);

            view.m_ScribesRoles.m_Button.Interactable = false;
            view.m_ScribesRoles.m_ScrollsIconButton.Interactable = false;
            view.m_ScribesRoles.m_ScrollsIconButton.OnLeftClick.RemoveAllListeners();
            view.m_ScribesRoles.m_ScrollsIconButton.OnLeftClickNotInteractable.RemoveAllListeners();
            DisablePortraits(view.m_ScribesRoles.m_FirstPortraitsView);
        }

        private static void DisablePortraits(RestRolesPortraitsPCView view)
        {
            view.m_PrimaryUnitButton.Interactable = false;
            view.m_SecondaryUnitButton.Interactable = false;
        }
    }
}
