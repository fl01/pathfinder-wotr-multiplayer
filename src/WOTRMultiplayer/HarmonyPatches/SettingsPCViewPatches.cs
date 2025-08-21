using System;
using System.Reflection;
using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.Settings;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches
{
    [HarmonyPatch]
    public class SettingsPCViewPatches
    {
        [HarmonyPatch(typeof(SettingsPCView), nameof(SettingsPCView.Initialize))]
        [HarmonyPostfix]
        public static void SettingsPCView_Initialize_Prefix(SettingsPCView __instance)
        {
            Main.GetLogger<BlueprintesCachePatches>().LogInformation("Applying patch. MethodName={MethodName}", MethodBase.GetCurrentMethod().Name);

            try
            {
                Main.Multiplayer.Factory.StoreDropdownPrefab(__instance.m_SettingsViews.m_SettingsEntityDropdownViewPrefab);
            }
            catch (Exception ex)
            {
                Main.GetLogger<SettingsPCViewPatches>().LogError(ex, "Unable to apply SettingsPCView patch");
                throw;
            }
        }
    }
}
