using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.Common.MessageModal;

namespace WOTRMultiplayer.HarmonyPatches.SkipTime
{
    [HarmonyPatch]
    public class SkipTimePCViewPatches
    {
        [HarmonyPatch(typeof(SkipTimePCView), nameof(SkipTimePCView.BindViewImplementation))]
        [HarmonyPostfix]
        public static void SkipTimePCView_BindViewImplementation_Postfix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnSkipTimeOpened();
        }

        [HarmonyPatch(typeof(SkipTimePCView), nameof(SkipTimePCView.Close))]
        [HarmonyPrefix]
        public static bool SkipTimePCView_Close_Prefix(SkipTimePCView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            // UI could be locked out due to network sync
            if (!__instance.m_CloseButton.Interactable)
            {
                return false;
            }

            Main.Multiplayer.OnSkipTimeClosed();
            return true;
        }

        [HarmonyPatch(typeof(SkipTimePCView), nameof(SkipTimePCView.SetCounterText))]
        [HarmonyPrefix]
        public static void SkipTimePCView_SetCounterText_Prefix(SkipTimePCView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var hours = __instance.m_HoursSlider.value;
            Main.Multiplayer.OnSkipTimeHoursChanged(hours);
        }

        [HarmonyPatch(typeof(SkipTimePCView), nameof(SkipTimePCView.SkipTime))]
        [HarmonyPrefix]
        public static void SkipTimePCView_SkipTime_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            Main.Multiplayer.OnSkipTimeStarted();
        }
    }
}
