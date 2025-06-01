using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.EscMenu;
using Microsoft.Extensions.Logging;
using UnityEngine;
using WOTRMultiplayer.UI;

namespace WOTRMultiplayer.HarmonyPatches
{
    [HarmonyPatch]
    public class EscMenuPCViewPatches
    {
        [HarmonyPatch(typeof(EscMenuPCView), nameof(EscMenuPCView.BindViewImplementation))]
        [HarmonyPrefix]
        public static void EscMenuPCView_BindViewImplementation_Postfix(EscMenuPCView __instance)
        {
            var logger = Main.GetLogger<EscMenuPCViewPatches>();
            logger.LogInformation("Applying patch [{patchName}]", nameof(EscMenuPCView_BindViewImplementation_Postfix));
            try
            {
                var mainMenu = __instance.transform.Find($"Window/ButtonBlock/{UIFactory.MultiplayerMenuObjectName}")?.gameObject;
                if (mainMenu == null && Main.Multiplayer.IsActive)
                {
                    logger.LogInformation("MultiplayerMenu doesn't exist.");
                    Main.Multiplayer.CreateEscMenuItem(__instance);
                }
                else if (mainMenu != null && !Main.Multiplayer.IsActive)
                {
                    logger.LogInformation("Deleting lobby menu item since current game is not a multiplayer one");
                    Object.DestroyImmediate(mainMenu);
                }
            }
            catch (System.Exception ex)
            {
                logger.LogError(ex, "Unable to apply patch");
                throw;
            }
        }
    }
}
