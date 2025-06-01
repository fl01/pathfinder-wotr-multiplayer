using HarmonyLib;
using Kingmaker.UI;
using Kingmaker.UI.MVVM._VM.EscMenu;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches
{
    [HarmonyPatch]
    public class EscMenuVMPatches
    {
        [HarmonyPatch(typeof(EscMenuVM), nameof(EscMenuVM.OnQuitToMainMenuAction))]
        [HarmonyPrefix]
        public static void EscMenuVM_OnQuitToMainMenuAction_Prefix(MessageModalBase.ButtonType buttonType)
        {
            var logger = Main.GetLogger<EscMenuVMPatches>();
            logger.LogInformation("Applying patch [{patchName}]", nameof(EscMenuVM_OnQuitToMainMenuAction_Prefix));
            if (buttonType == MessageModalBase.ButtonType.Yes)
            {
                logger.LogInformation("quit to main menu");
                Main.Multiplayer.TerminateMultiplayer();
            }
        }
    }
}
