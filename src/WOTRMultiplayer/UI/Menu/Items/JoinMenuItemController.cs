using Kingmaker.PubSubSystem;
using Kingmaker.UI;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.Controls.Button;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WOTRMultiplayer.Abstractions.MP;
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
        public const string JoinLobbyActionMenuObjectName = "JoinLobbyActionMenu";
        public const string JoinLobbyControlsMenuObjectName = "JoinLobbyControlsMenu";
        public const string ServerAddressInputObjectName = "ServerAddressInput";

        public const string LobbyControlsMenuObjectName = "LobbyControlsMenu";
        public const string LobbyControlsMenuReadyButtonObjectName = "ReadyButton";
        public const string LobbyControlsMenuLeaveButtonObjectName = "LeaveButton";

        private readonly ILogger<JoinMenuItemController> _logger;
        private readonly ILobbyWindowController _lobbyWindowController;
        private readonly IUIFactory _uIFactory;
        private readonly IMultiplayerClient _multiplayerClient;
        private GameObject _menuContent;

        protected override GameObject MenuContent => _menuContent;

        protected GameObject JoinLobbyControlsObject => _menuContent.transform
            .Find(JoinLobbyScreenObjectName)
            .Find(JoinLobbyActionMenuObjectName)
            .Find(JoinLobbyControlsMenuObjectName)
            .gameObject;

        protected GameObject ServerAddressObject => JoinLobbyControlsObject.transform
            .Find(ServerAddressInputObjectName)
            .gameObject;

        protected GameObject LobbyControls => _menuContent.transform
            .Find(JoinLobbyScreenObjectName)
            .Find(JoinLobbyActionMenuObjectName)
            .Find(LobbyControlsMenuObjectName)
            .gameObject;

        protected GameObject ReadyButtonObject => LobbyControls.transform
            .Find(LobbyControlsMenuReadyButtonObjectName)
            .gameObject;

        protected GameObject LeaveButtonObject => LobbyControls.transform
            .Find(LobbyControlsMenuLeaveButtonObjectName)
            .gameObject;

        public JoinMenuItemController(
            ILogger<JoinMenuItemController> logger,
            ILobbyWindowController lobbyWindowController,
            IMultiplayerClient multiplayerClient,
            IUIFactory uIFactory)
            : base(logger)
        {
            _logger = logger;
            _lobbyWindowController = lobbyWindowController;
            _uIFactory = uIFactory;
            _multiplayerClient = multiplayerClient;
        }

        public override void Activate()
        {
            _logger.LogInformation("Trying to activate");

            if (IsActive)
            {
                return;
            }

            _lobbyWindowController.SetActiveOwner(LobbyWindowOwner.JoinMenu);
            base.Activate();
        }

        protected override void InitializeInternal(GameObject baseLayout)
        {
            var label = MenuItem.GetComponentInChildren<TextMeshProUGUI>();
            label.SetText(StringConsts.MultiplayerWindow.JoinMenuLabel);

            _menuContent = UnityEngine.Object.Instantiate(baseLayout, baseLayout.transform);
            _menuContent.name = JoinMenuItemContentObjectName;
            _menuContent.AddComponent<VerticalLayoutGroup>().padding = new RectOffset(0, 0, 25, 0);
            _menuContent.CleanupAllChildren();
            var menuContentRect = _menuContent.GetComponent<RectTransform>();
            menuContentRect.sizeDelta = new Vector2(menuContentRect.sizeDelta.x * 0.4f, menuContentRect.sizeDelta.y * 0.88f);

            var content = _uIFactory.CreateDefaultGameObject(_menuContent.transform);
            content.name = JoinLobbyScreenObjectName;
            content.AddComponent<VerticalLayoutGroup>();

            var lobbyWindow = _uIFactory.CreateDefaultGameObject(content.transform);
            var lobbyWindowLayout = lobbyWindow.AddComponent<LayoutElement>();
            lobbyWindowLayout.preferredHeight = menuContentRect.sizeDelta.y;
            lobbyWindow.AddComponent<VerticalLayoutGroup>();
            lobbyWindow.name = "LobbyWindow";
            var lobbyWindowRect = lobbyWindow.GetComponent<RectTransform>();
            lobbyWindowRect.sizeDelta = menuContentRect.sizeDelta;
            _lobbyWindowController.InitializeContent(LobbyWindowOwner.JoinMenu, lobbyWindow.transform);

            var actionMenuContainer = _uIFactory.CreateDefaultGameObject(content.transform);
            actionMenuContainer.name = JoinLobbyActionMenuObjectName;

            actionMenuContainer.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
            actionMenuContainer.AddComponent<HorizontalLayoutGroup>();
            var actionMenuContainerLayout = actionMenuContainer.AddComponent<LayoutElement>();
            actionMenuContainerLayout.preferredHeight = menuContentRect.sizeDelta.y * 0.07f;

            // input + button ?
            var joinLobbyControlsMenu = _uIFactory.CreateDefaultGameObject(actionMenuContainer.transform);
            joinLobbyControlsMenu.name = JoinLobbyControlsMenuObjectName;
            joinLobbyControlsMenu.AddComponent<HorizontalLayoutGroup>();

            var joinLobbyButtonObject = _uIFactory.CreateButton(joinLobbyControlsMenu.transform);
            var joinLobbyButtonObjectLayout = joinLobbyButtonObject.AddComponent<LayoutElement>();
            joinLobbyButtonObjectLayout.preferredWidth = menuContentRect.sizeDelta.x * 0.2f;
            joinLobbyButtonObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            joinLobbyButtonObject.GetComponentInChildren<TextMeshProUGUI>().SetText(StringConsts.MultiplayerWindow.JoinMenu.JoinButtonLabel);
            var button = joinLobbyButtonObject.GetComponent<OwlcatButton>();
            button.OnLeftClick.AddListener(OnJoinButtonClicked);

            var serverInfoInputObject = _uIFactory.CreateInput(joinLobbyControlsMenu.transform);
            serverInfoInputObject.name = ServerAddressInputObjectName;
            var serverPlaceholder = serverInfoInputObject.transform.Find(UIFactory.InputPlaceholderObjectName);
            serverPlaceholder.GetComponent<TextMeshProUGUI>().SetText(StringConsts.MultiplayerWindow.JoinMenu.ServerInputPlaceholder);
            var serverInfoInput = serverInfoInputObject.transform.Find(UIFactory.InputLabelObjectName);
            serverInfoInput.GetComponent<TextMeshProUGUI>().overflowMode = TextOverflowModes.Truncate;
            serverInfoInput.GetComponent<TextMeshProUGUI>().maxVisibleCharacters = 21;

            // leave + ready buttons?
            var lobbyControlsMenu = _uIFactory.CreateDefaultGameObject(actionMenuContainer.transform);
            lobbyControlsMenu.name = LobbyControlsMenuObjectName;
            lobbyControlsMenu.SetActive(false);
            lobbyControlsMenu.AddComponent<HorizontalLayoutGroup>();

            var readyButtonObject = _uIFactory.CreateButton(lobbyControlsMenu.transform);
            readyButtonObject.name = LobbyControlsMenuReadyButtonObjectName;
            var readyButtonObjectLayout = readyButtonObject.AddComponent<LayoutElement>();
            readyButtonObjectLayout.preferredWidth = menuContentRect.sizeDelta.x * 0.2f;
            readyButtonObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            readyButtonObject.GetComponentInChildren<TextMeshProUGUI>().SetText(StringConsts.MultiplayerWindow.JoinMenu.ReadyNotReadyButtonLabel);
            var readyButton = readyButtonObject.GetComponent<OwlcatButton>();
            readyButton.OnLeftClick.AddListener(OnReadyButtonClicked);

            var leaveButtonObject = _uIFactory.CreateButton(lobbyControlsMenu.transform);
            leaveButtonObject.name = LobbyControlsMenuLeaveButtonObjectName;
            var leaveButtonObjectLayout = leaveButtonObject.AddComponent<LayoutElement>();
            leaveButtonObjectLayout.preferredWidth = menuContentRect.sizeDelta.x * 0.2f;
            leaveButtonObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            leaveButtonObject.GetComponentInChildren<TextMeshProUGUI>().SetText(StringConsts.MultiplayerWindow.JoinMenu.LeaveButtonLabel);
            var leaveButton = leaveButtonObject.GetComponent<OwlcatButton>();
            leaveButton.OnLeftClick.AddListener(OnLeaveButtonClicked);
        }

        private void OnReadyButtonClicked()
        {
            var isReady = _multiplayerClient.ReadyChanged();
            var label = isReady ? StringConsts.MultiplayerWindow.HostMenu.ReadyButtonLabel
                : StringConsts.MultiplayerWindow.HostMenu.ReadyNotReadyButtonLabel;
            ReadyButtonObject.GetComponentInChildren<TextMeshProUGUI>().SetText(label);
        }

        private void OnLeaveButtonClicked()
        {
            _multiplayerClient.Dispose();
            JoinLobbyControlsObject.SetActive(true);
            LobbyControls.SetActive(false);
        }

        private void OnJoinButtonClicked()
        {
            _logger.LogInformation("Join button clicked");
            var rawAddress = ServerAddressObject.transform.Find(UIFactory.InputLabelObjectName).GetComponent<TextMeshProUGUI>().text;
            // thank you for zero-width space
            var address = rawAddress.Trim('\u200B').Trim();
            var result = _multiplayerClient.Join(address, new MP.MultiplayerSettings());
            if (!result.IsOk)
            {
                EventBus.RaiseEvent<IMessageModalUIHandler>(delegate (IMessageModalUIHandler w)
                {
                    w.HandleOpen(result.Message, MessageModalBase.ModalType.Message, null, null, null, null, null, null, null, 0, uint.MaxValue, null);
                }, true);
                return;
            }

            JoinLobbyControlsObject.SetActive(false);
            LobbyControls.SetActive(true);
        }

        protected override ModalActionConfirmation GetDeactivationConfirmationInternal()
        {
            if (!_multiplayerClient.IsActive)
            {
                return base.GetDeactivationConfirmationInternal();
            }

            return new ModalActionConfirmation
            {
                Text = StringConsts.MultiplayerWindow.JoinMenu.LeaveGameMessage
            };
        }
    }
}
