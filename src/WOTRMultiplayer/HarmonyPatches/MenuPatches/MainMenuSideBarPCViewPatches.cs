using System;
using HarmonyLib;
using Kingmaker.PubSubSystem;
using Kingmaker.UI.MVVM._PCView.MainMenu;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Entities;

namespace WOTRMultiplayer.HarmonyPatches.MenuPatches
{
    [HarmonyPatch]
    public class MainMenuSideBarPCViewPatches
    {
        [HarmonyPatch(typeof(MainMenuSideBarPCView), nameof(MainMenuSideBarPCView.BindViewImplementation))]
        [HarmonyPrefix]
        public static void MainMenuSideBarPCView_BindViewImplementation_Prefix(MainMenuSideBarPCView __instance)
        {
            try
            {
                Main.GetLogger<MainMenuSideBarPCViewPatches>().LogInformation("Initializing multiplayer");

                var menuButtons = __instance.transform.GetChild(0);
                for (int menuIndex = 0; menuIndex < menuButtons.childCount; menuIndex++)
                {
                    var menuButton = menuButtons.GetChild(menuIndex).gameObject;
                    if (string.Equals(menuButton.name, "Settings"))
                    {
                        var context = new InitializeMultiplayerContext(menuButton, menuButtons.transform);
                        var isOk = Main.Multiplayer.InitializeMultiplayer(context);
                        if (!isOk)
                        {
                            Main.GetLogger<MainMenuSideBarPCViewPatches>().LogError("Unable to inject multiplayer menu");
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Main.GetLogger<MainMenuSideBarPCViewPatches>().LogError(ex, "Unable to apply MainMenuSideBarPCView patch");
                throw;
            }
        }

        [HarmonyPatch(typeof(MainMenuSideBarPCView), nameof(MainMenuSideBarPCView.BindViewImplementation))]
        [HarmonyPostfix]
        public static void MainMenuSideBarPCView_BindViewImplementation_Postfix()
        {
            if (BlueprintesCachePatches.LastInitError != null)
            {
                EventBus.RaiseEvent<IMessageModalUIHandler>(x => x.HandleOpen(BlueprintesCachePatches.LastInitError));
            }
        }
    }
}
