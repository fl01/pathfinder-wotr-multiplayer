using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.EscMenu;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.Controls.Button;
using UnityEngine;
using WOTRMultiplayer.UI;

namespace WOTRMultiplayer.HarmonyPatches.MenuPatches
{
    [HarmonyPatch]
    public class EscMenuPCViewPatches
    {
        [HarmonyPatch(typeof(EscMenuPCView), nameof(EscMenuPCView.BindViewImplementation))]
        [HarmonyPrefix]
        public static void EscMenuPCView_BindViewImplementation_Postfix(EscMenuPCView __instance)
        {
            var logger = Main.GetLogger<EscMenuPCViewPatches>();
            logger.LogInformation("Applying patch [{PatchName}]", nameof(EscMenuPCView_BindViewImplementation_Postfix));
            try
            {
                var mainMenu = __instance.transform.Find($"Window/ButtonBlock/{UIFactory.MultiplayerMenuObjectName}")?.gameObject;
                if (mainMenu == null && Main.Multiplayer.IsActive)
                {
                    Main.Multiplayer.InitializeEscMenuLobbyWindow();
                    SetPhotoModeButtonState(__instance, false);
                }
                else if (mainMenu != null && !Main.Multiplayer.IsActive)
                {
                    logger.LogInformation("Deleting lobby menu item since current game is not a multiplayer one");
                    Object.DestroyImmediate(mainMenu);
                    SetPhotoModeButtonState(__instance, true);
                }
            }
            catch (System.Exception ex)
            {
                logger.LogError(ex, "Unable to apply patch");
                throw;
            }
        }

        private static void SetPhotoModeButtonState(EscMenuPCView view, bool isInteractable)
        {
            var photoMode = view.transform.Find("Window/ButtonBlock/PhotoModeButton")?.gameObject;
            if (photoMode != null)
            {
                photoMode.GetComponent<OwlcatButton>().Interactable = isInteractable;
            }
        }
    }
}
