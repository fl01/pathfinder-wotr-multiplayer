using System;
using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.Settings;
using Microsoft.Extensions.Logging;
using Serilog;

namespace WOTRMultiplayer.HarmonyPatches
{
    [HarmonyPatch]
    public class SettingsPCViewPatches
    {
        [HarmonyPatch(typeof(SettingsPCView), nameof(SettingsPCView.Initialize))]
        [HarmonyPostfix]
        public static void SettingsPCView_Initialize_Prefix(SettingsPCView __instance)
        {
            var logger = Main.GetLogger<SettingsPCViewPatches>();
            logger.LogInformation("Applying patch [{patchName}]", nameof(SettingsPCView_Initialize_Prefix));

            try
            {
                Main.Multiplayer.Factory.StoreDropdownPrefab(__instance.m_SettingsViews.m_SettingsEntityDropdownViewPrefab);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Unable to apply SettingsPCView patch");
                throw;
            }
        }
    }
}
