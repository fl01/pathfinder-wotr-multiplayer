using System;
using HarmonyLib;
using Kingmaker.PubSubSystem;
using Kingmaker.UI.MVVM;
using Kingmaker.UI.MVVM._PCView.MainMenu;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches.MenuPatches
{
    [HarmonyPatch]
    public class MainMenuInitializationPatches
    {
        [HarmonyPatch(typeof(RootUIContext), nameof(RootUIContext.InitializeUiScene))]
        [HarmonyPostfix]
        public static void RootUIContext_InitializeUiScene_Postfix(string loadedUIScene)
        {
            try
            {
                if (loadedUIScene != "UI_MainMenu_Scene")
                {
                    return;
                }

                Main.Multiplayer.Initialize();
            }
            catch (Exception ex)
            {
                Main.GetLogger<MainMenuInitializationPatches>().LogError(ex, "Unable to apply MainMenuSideBarPCView patch");
                throw;
            }
        }

        [HarmonyPatch(typeof(MainMenuSideBarPCView), nameof(MainMenuSideBarPCView.BindViewImplementation))]
        [HarmonyPostfix]
        public static void MainMenuSideBarPCView_BindViewImplementation_Postfix()
        {
            if (BlueprintsCachePatches.LastInitError != null)
            {
                EventBus.RaiseEvent<IMessageModalUIHandler>(x => x.HandleOpen(BlueprintsCachePatches.LastInitError));
            }
        }
    }
}
