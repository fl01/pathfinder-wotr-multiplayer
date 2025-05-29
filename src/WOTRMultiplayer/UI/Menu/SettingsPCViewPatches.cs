using System;
using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.Settings;
using Serilog;

namespace WOTRMultiplayer.UI.Menu
{
    [HarmonyPatch]
    public class SettingsPCViewPatches
    {
        [HarmonyPatch(typeof(SettingsPCView), "Initialize")]
        [HarmonyPostfix]
        public static void SettingsPCView_Initialize_Prefix(SettingsPCView __instance)
        {
            Log.Logger.Information("{methodName}: Applying", nameof(SettingsPCView_Initialize_Prefix));

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
