using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Controllers.Menu;
using WOTRMultiplayer.Extensions;

namespace WOTRMultiplayer.UI.Menu.Items
{
    public class JoinMenuItemController : MenuItemController, IJoinMenuItemController
    {
        public const string JoinMenuItemContentObjectName = "JoinMenuItemContent";
        private readonly ILogger<JoinMenuItemController> _logger;
        private readonly ILobbyWindowController _lobbyWindowController;

        private GameObject _menuContent;

        protected override GameObject MenuContent => _menuContent;

        public JoinMenuItemController(
            ILogger<JoinMenuItemController> logger,
            ILobbyWindowController lobbyWindowController)
            : base(logger)
        {
            _logger = logger;
            _lobbyWindowController = lobbyWindowController;
        }

        public override void Activate()
        {
            _logger.LogInformation("Trying to activate");

            if (IsActive)
            {
                return;
            }

            base.Activate();
        }

        protected override void InitializeInternal(GameObject baseLayout)
        {
            var label = this.MenuItem.GetComponentInChildren<TextMeshProUGUI>();
            label.SetText(StringConsts.MultiplayerWindow.JoinMenuLabel);

            _menuContent = UnityEngine.Object.Instantiate(baseLayout, baseLayout.transform);
            _menuContent.name = JoinMenuItemContentObjectName;
            _menuContent.CleanupAllChildren();
        }
    }
}
