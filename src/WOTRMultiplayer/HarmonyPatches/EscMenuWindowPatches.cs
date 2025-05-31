using HarmonyLib;
using Kingmaker.Localization;
using Kingmaker.UI.MVVM._PCView.EscMenu;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.Controls.Button;
using TMPro;
using UnityEngine;

namespace WOTRMultiplayer.HarmonyPatches
{
    [HarmonyPatch]
    public class EscMenuWindowPatches
    {
        public const string multiplayerMenuObjectName = "MultiplayerLobbyButton";

        [HarmonyPatch(typeof(EscMenuPCView), "BindViewImplementation")]
        [HarmonyPrefix]
        public static void EscMenuPCView_BindViewImplementation_Postfix(EscMenuPCView __instance)
        {
            var logger = Main.GetLogger<EscMenuWindowPatches>();
            logger.LogInformation("Applying patch [{patchName}]", nameof(EscMenuPCView_BindViewImplementation_Postfix));
            var mainMenu = __instance.transform.Find($"Window/ButtonBlock/{multiplayerMenuObjectName}")?.gameObject;
            if (mainMenu == null && Main.Multiplayer.IsActive)
            {
                logger.LogInformation("MultiplayerMenu doesn't exist.");
                mainMenu = CreateMultiplayerMenu(__instance, logger);
            }
            else if (mainMenu != null && !Main.Multiplayer.IsActive)
            {
                Object.DestroyImmediate(mainMenu);
            }
        }

        private static GameObject CreateMultiplayerMenu(EscMenuPCView view, ILogger<EscMenuWindowPatches> logger)
        {
            logger.LogInformation("Creating MultiplayerMenu");
            var optionsButton = view.transform.Find("Window/ButtonBlock/OptionsButton");
            var multiplayerMenu = Object.Instantiate(optionsButton.gameObject, optionsButton.transform.parent);
            multiplayerMenu.transform.SetSiblingIndex(optionsButton.GetSiblingIndex());
            multiplayerMenu.name = multiplayerMenuObjectName;
            var textObject = multiplayerMenu.transform.Find("Text");
            Object.DestroyImmediate(textObject.GetComponent<LocalizedUIText>());
            textObject.GetComponent<TextMeshProUGUI>().SetText("Multiplayer Lobby");
            var button = multiplayerMenu.GetComponent<OwlcatButton>();
            button.OnLeftClick.RemoveAllListeners();
            button.OnLeftClick.AddListener(() => logger.LogInformation("Show lobby"));
            return multiplayerMenu;
        }
    }
}
