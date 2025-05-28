using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Controllers.Menu;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.UI.Lobby;

namespace WOTRMultiplayer.UI.Menu.Items
{
    public class JoinMenuItemController : MenuItemController, IJoinMenuItemController
    {
        public const string JoinMenuItemContentObjectName = "JoinMenuItemContent";
        public const string JoinLobbyScreenObjectName = "JoinLobbyScreen";
        public const string LobbyWindowObjectName = "MultiplayerLobby";

        private readonly ILogger<JoinMenuItemController> _logger;
        private readonly ILobbyWindowController _lobbyWindowController;
        private readonly IUIFactory _uIFactory;
        private GameObject _menuContent;

        protected override GameObject MenuContent => _menuContent;

        public JoinMenuItemController(
            ILogger<JoinMenuItemController> logger,
            ILobbyWindowController lobbyWindowController,
            IUIFactory uIFactory)
            : base(logger)
        {
            _logger = logger;
            _lobbyWindowController = lobbyWindowController;
            _uIFactory = uIFactory;
        }

        public override void Activate()
        {
            _logger.LogInformation("Trying to activate");

            if (IsActive)
            {
                return;
            }

            _lobbyWindowController.SetActiveOwner(LobbyWindowOwner.JoinMenu);
            _lobbyWindowController.UpdateServerInfo("ZAZAZA");

            base.Activate();
        }

        protected override void InitializeInternal(GameObject baseLayout)
        {
            var label = this.MenuItem.GetComponentInChildren<TextMeshProUGUI>();
            label.SetText(StringConsts.MultiplayerWindow.JoinMenuLabel);

            _menuContent = UnityEngine.Object.Instantiate(baseLayout, baseLayout.transform);
            _menuContent.name = JoinMenuItemContentObjectName;
            _menuContent.CleanupAllChildren();
            var menuContentRect = _menuContent.GetComponent<RectTransform>();
            menuContentRect.sizeDelta = new Vector2(menuContentRect.sizeDelta.x * 0.4f, menuContentRect.sizeDelta.y * 0.85f);

            var content = _uIFactory.CreateDefaultGameObject(_menuContent.transform);
            content.AddComponent<Image>().color = Color.grey;
            content.name = "JoinLobbyScreen";
            content.AddComponent<VerticalLayoutGroup>();

            var lobbyWindow = _uIFactory.CreateDefaultGameObject(content.transform);
            var aaaa = lobbyWindow.AddComponent<LayoutElement>();
            aaaa.preferredHeight = menuContentRect.sizeDelta.y;
            lobbyWindow.AddComponent<VerticalLayoutGroup>();
            lobbyWindow.AddComponent<Image>().color = Color.red;
            lobbyWindow.name = "LobbyWindow";
            var lobbyWindowRect = lobbyWindow.GetComponent<RectTransform>();
            lobbyWindowRect.sizeDelta = menuContentRect.sizeDelta;
            _lobbyWindowController.InitializeContent(LobbyWindowOwner.JoinMenu, lobbyWindow.transform);

            var actionMenuContainer = _uIFactory.CreateDefaultGameObject(content.transform);
            actionMenuContainer.AddComponent<Image>().color = Color.green;
            actionMenuContainer.AddComponent<HorizontalLayoutGroup>();
            var actionMenuContainerLayout = actionMenuContainer.AddComponent<LayoutElement>();
            actionMenuContainerLayout.preferredHeight = menuContentRect.sizeDelta.y * 0.1f;

            // input + button ?
            var joinLobbyControlsMenu = _uIFactory.CreateDefaultGameObject(actionMenuContainer.transform);
            joinLobbyControlsMenu.AddComponent<HorizontalLayoutGroup>();

            // leave + ready buttons?
            var lobbyControlsMenu = _uIFactory.CreateDefaultGameObject(actionMenuContainer.transform);
            lobbyControlsMenu.AddComponent<HorizontalLayoutGroup>();
        }
    }
}
