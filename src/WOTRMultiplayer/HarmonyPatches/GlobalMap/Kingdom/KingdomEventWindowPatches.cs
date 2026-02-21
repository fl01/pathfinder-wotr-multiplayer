using HarmonyLib;
using Kingmaker.Kingdom.UI;
using Kingmaker.UI.Kingdom;
using Kingmaker.Utility;
using Owlcat.Runtime.UI.Controls.Button;
using WOTRMultiplayer.Entities.GlobalMap.Kingdom;

namespace WOTRMultiplayer.HarmonyPatches.GlobalMap.Kingdom
{
    [HarmonyPatch]
    public class KingdomEventWindowPatches
    {
        [HarmonyPatch(typeof(KingdomUIEventWindow), nameof(KingdomUIEventWindow.OnNextClick))]
        [HarmonyPrefix]
        public static bool KingdomNaviElement_OnNextClick_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.CanControlGlobalMap();
            return canContinue;
        }

        [HarmonyPatch(typeof(KingdomUIEventWindow), nameof(KingdomUIEventWindow.OnPrevClick))]
        [HarmonyPrefix]
        public static bool KingdomNaviElement_OnPrevClick_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.CanControlGlobalMap();
            return canContinue;
        }

        [HarmonyPatch(typeof(KingdomUIEventWindow), nameof(KingdomUIEventWindow.OnCrossPressed))]
        [HarmonyPrefix]
        public static bool KingdomNaviElement_OnCrossPressed_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.CanControlGlobalMap();
            return canContinue;
        }

        [HarmonyPatch(typeof(KingdomEventHandCartController), nameof(KingdomEventHandCartController.OnClick))]
        [HarmonyPrefix]
        public static bool KingdomEventHandCartController_OnClick_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.CanControlGlobalMap();
            return canContinue;
        }

        /// <summary>
        /// produces x2 events for (toggle off/on pair), but it's fine
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPatch(typeof(KingdomUIEventWindowFooter), nameof(KingdomUIEventWindowFooter.UpdateView))]
        [HarmonyPostfix]
        public static void KingdomUIEventWindowFooter_UpdateView_Postfix(KingdomUIEventWindowFooter __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var canControl = Main.Multiplayer.CanControlGlobalMap();
            __instance.m_StartEvent.Interactable = __instance.m_StartEvent.Interactable && canControl;
            __instance.m_Cancel.Interactable = __instance.m_Cancel.Interactable && canControl;
            __instance.m_Close.Interactable = __instance.m_Close.Interactable && canControl;
            __instance.m_Drop.Interactable = __instance.m_Drop.Interactable && canControl;

            if (__instance.m_Solutions.m_Solutions.Count == 0)
            {
                return;
            }

            var solution = GetEventSolution(__instance);
            Main.Multiplayer.OnKingdomEventSolutionSelected(solution);
        }

        [HarmonyPatch(typeof(KingdomUIEventWindowFooter), nameof(KingdomUIEventWindowFooter.OnStart))]
        [HarmonyPrefix]
        public static void KingdomUIEventWindowFooter_OnStart_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnKingdomEventStarted();
        }

        [HarmonyPatch(typeof(KingdomEventUIView), nameof(KingdomEventUIView.DropEvent))]
        [HarmonyPrefix]
        public static void KingdomEventUIView_DropEvent_Prefix(KingdomEventUIView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var kingdomEvent = Main.Mapper.Map<NetworkKingdomEvent>(__instance);
            Main.Multiplayer.OnKingdomEventDropped(kingdomEvent);
        }

        [HarmonyPatch(typeof(KingdomUIEventWindowFooter), nameof(KingdomUIEventWindowFooter.OnCancelEvent))]
        [HarmonyPrefix]
        public static void KingdomUIEventWindowFooter_OnCancelEvent_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnKingdomEventCancelled();
        }

        [HarmonyPatch(typeof(KingdomUIEventWindowFooterSolutionGroup), nameof(KingdomUIEventWindowFooterSolutionGroup.Initialize))]
        [HarmonyPostfix]
        public static void KingdomUIEventWindowFooterSolutionGroup_Initialize_Postfix(KingdomUIEventWindowFooterSolutionGroup __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var canControl = Main.Multiplayer.CanControlGlobalMap();
            if (!canControl)
            {
                foreach (var solution in __instance.m_Solutions)
                {
                    solution.Toggle.interactable = false;
                }
            }
        }

        [HarmonyPatch(typeof(KingdomUISettlementWindow), nameof(KingdomUISettlementWindow.Show))]
        [HarmonyPostfix]
        public static void KingdomUISettlementWindow_Show_Postfix(KingdomUISettlementWindow __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var canControl = Main.Multiplayer.CanControlGlobalMap();
            __instance.m_Enter.Interactable = __instance.m_Enter.Interactable && canControl;
            var upgradeButton = __instance.m_UpgradeBlock?.GetComponentInChildren<OwlcatButton>();
            if (upgradeButton != null)
            {
                upgradeButton.Interactable = upgradeButton.Interactable && canControl;
            }
        }

        private static NetworkKingdomEventSolution GetEventSolution(KingdomUIEventWindowFooter footer)
        {
            if (footer.CurrentEventSolution == null)
            {
                return null;
            }

            var selectedSolution = footer.m_Solutions.m_Solutions.FindOrDefault(s => s.Toggle.isOn);
            var index = footer.m_Solutions.m_Solutions.IndexOf(selectedSolution);
            var solution = new NetworkKingdomEventSolution
            {
                Index = index,
                Name = selectedSolution.EventSolution.m_SolutionText
            };
            return solution;
        }
    }
}
