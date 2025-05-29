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
using WOTRMultiplayer.Unity;

namespace WOTRMultiplayer.UI.Menu.Items
{
    public class JoinMenuItemController : MenuItemController, IJoinMenuItemController
    {
        public const string RootContentScreenObjectName = "RootContentScreen";
        public const string JoinMenuItemContentObjectName = "JoinMenuItemContent";
        public const string JoinLobbyControlsMenuObjectName = "JoinLobbyControlsMenu";
        public const string ServerAddressInputObjectName = "ServerAddressInput";
        public const string JoinServerButtonObjectName = "JoinServerButton";

        public const string LobbyControlsMenuObjectName = "LobbyControlsMenu";
        public const string LobbyControlsMenuReadyButtonObjectName = "ReadyButton";
        public const string LobbyControlsMenuLeaveButtonObjectName = "LeaveButton";

        public const string LobbyWindowObjectName = "LobbyWindow";

        private readonly ILogger<JoinMenuItemController> _logger;
        private readonly IMainThreadAccessor _mainThreadAccessor;
        private readonly IUIFactory _uIFactory;
        private readonly IMultiplayerClient _multiplayerClient;
        private GameObject _menuContent;

        protected override GameObject MenuContent => _menuContent;
        protected override LobbyWindowOwner Owner => LobbyWindowOwner.JoinMenu;

        protected GameObject JoinLobbyControlsObject => _menuContent.transform
            .Find(RootContentScreenObjectName)
            .Find(JoinLobbyControlsMenuObjectName)
            .gameObject;

        protected GameObject JoinButtonObject => JoinLobbyControlsObject.transform
            .Find(JoinServerButtonObjectName)
            .gameObject;

        protected GameObject ServerAddressObject => JoinLobbyControlsObject.transform
            .Find(ServerAddressInputObjectName)
            .gameObject;

        protected GameObject LobbyControls => _menuContent.transform
            .Find(RootContentScreenObjectName)
            .Find(LobbyControlsMenuObjectName)
            .gameObject;

        protected GameObject LobbyWindow => _menuContent.transform
            .Find(RootContentScreenObjectName)
            .Find(LobbyWindowObjectName)
            .gameObject;

        protected GameObject ReadyButtonObject => LobbyControls.transform
            .Find(LobbyControlsMenuReadyButtonObjectName)
            .gameObject;

        protected GameObject LeaveButtonObject => LobbyControls.transform
            .Find(LobbyControlsMenuLeaveButtonObjectName)
            .gameObject;

        public JoinMenuItemController(
            ILogger<JoinMenuItemController> logger,
            IMainThreadAccessor mainThreadAccessor,
            ILobbyWindowController lobbyWindowController,
            IMultiplayerClient multiplayerClient,
            IUIFactory uIFactory)
            : base(logger, lobbyWindowController)
        {
            _logger = logger;
            _mainThreadAccessor = mainThreadAccessor;
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

            Lobby.SetActiveOwner(LobbyWindowOwner.JoinMenu);
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
            content.name = RootContentScreenObjectName;
            content.AddComponent<VerticalLayoutGroup>();

            var lobbyWindow = _uIFactory.CreateDefaultGameObject(content.transform);
            lobbyWindow.name = LobbyWindowObjectName;
            var lobbyWindowLayout = lobbyWindow.AddComponent<LayoutElement>();
            lobbyWindowLayout.preferredHeight = menuContentRect.sizeDelta.y;
            var lobbyWindowVertical = lobbyWindow.AddComponent<VerticalLayoutGroup>();
            lobbyWindowVertical.padding = new RectOffset(0, 0, 0, 20);
            var lobbyWindowRect = lobbyWindow.GetComponent<RectTransform>();
            lobbyWindowRect.sizeDelta = menuContentRect.sizeDelta;
            Lobby.InitializeContent(LobbyWindowOwner.JoinMenu, lobbyWindow.transform);

            // input + button ?
            var joinLobbyControlsMenu = _uIFactory.CreateDefaultGameObject(content.transform);
            joinLobbyControlsMenu.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            joinLobbyControlsMenu.name = JoinLobbyControlsMenuObjectName;
            joinLobbyControlsMenu.AddComponent<VerticalLayoutGroup>();
            joinLobbyControlsMenu.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var joinLobbyControlsMenuLayout = joinLobbyControlsMenu.AddComponent<LayoutElement>();
            joinLobbyControlsMenuLayout.preferredHeight = menuContentRect.sizeDelta.y * 0.10f;

            var serverInfoInputObject = _uIFactory.CreateInput(joinLobbyControlsMenu.transform);
            serverInfoInputObject.name = ServerAddressInputObjectName;
            var serverPlaceholder = serverInfoInputObject.transform.Find(UIFactory.InputPlaceholderObjectName);
            var serverPlaceholderInput = serverPlaceholder.GetComponent<TextMeshProUGUI>();
            serverPlaceholderInput.SetText(StringConsts.MultiplayerWindow.JoinMenu.ServerInputPlaceholder);
            serverPlaceholderInput.alignment = TextAlignmentOptions.Center;
            var serverInfoInputLabelObject = serverInfoInputObject.transform.Find(UIFactory.InputLabelObjectName);
            var serverInfoInput = serverInfoInputLabelObject.GetComponent<TextMeshProUGUI>();
            serverInfoInput.overflowMode = TextOverflowModes.Truncate;
            serverInfoInput.alignment = TextAlignmentOptions.Center;

            var joinLobbyButtonObject = _uIFactory.CreateButton(joinLobbyControlsMenu.transform);
            joinLobbyButtonObject.name = JoinServerButtonObjectName;
            var joinLobbyButtonObjectLayout = joinLobbyButtonObject.AddComponent<LayoutElement>();
            joinLobbyButtonObjectLayout.preferredWidth = menuContentRect.sizeDelta.x * 0.35f;
            joinLobbyButtonObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            joinLobbyButtonObject.GetComponentInChildren<TextMeshProUGUI>().SetText(StringConsts.MultiplayerWindow.JoinMenu.JoinButtonLabel);
            var button = joinLobbyButtonObject.GetComponent<OwlcatButton>();
            button.OnLeftClick.AddListener(OnJoinButtonClicked);

            // leave + ready buttons?
            var lobbyControlsMenu = _uIFactory.CreateDefaultGameObject(content.transform);
            lobbyControlsMenu.name = LobbyControlsMenuObjectName;
            lobbyControlsMenu.AddComponent<HorizontalLayoutGroup>();
            lobbyControlsMenu.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
            var lobbyControlsMenuLayout = lobbyControlsMenu.AddComponent<LayoutElement>();
            lobbyControlsMenuLayout.preferredHeight = menuContentRect.sizeDelta.y * 0.07f;

            var readyButtonObject = _uIFactory.CreateButton(lobbyControlsMenu.transform);
            readyButtonObject.name = LobbyControlsMenuReadyButtonObjectName;
            var readyButtonObjectLayout = readyButtonObject.AddComponent<LayoutElement>();
            readyButtonObjectLayout.preferredWidth = menuContentRect.sizeDelta.x * 0.2f;
            readyButtonObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
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

            _multiplayerClient.OnNetworkError = OnMultiplayerClientError;
            _multiplayerClient.OnConnected = OnMultiplayerClientConnected;
            _multiplayerClient.OnDisconnected = OnMultiplayerClientDisconnected;
        }

        private void OnMultiplayerClientDisconnected()
        {
            EventBus.RaiseEvent<IMessageModalUIHandler>(delegate (IMessageModalUIHandler w)
            {
                w.HandleOpen("You have been disconnected", MessageModalBase.ModalType.Message, null, null, null, null, null, null, null, 0, uint.MaxValue, null);
            }, true);

            _multiplayerClient.Dispose();
            ActivateJoinLobbyControls();
            Lobby.Reset();
        }

        private void OnMultiplayerClientConnected()
        {
            SetButtonActive(JoinButtonObject, true);
            ActivateLobbyControls();
        }

        private void OnMultiplayerClientError(string errorMessage)
        {
            _logger.LogError("Multiplayer client error");

            SetButtonActive(JoinButtonObject, true);

            EventBus.RaiseEvent<IMessageModalUIHandler>(delegate (IMessageModalUIHandler w)
            {
                w.HandleOpen(errorMessage, MessageModalBase.ModalType.Message, null, null, null, null, null, null, null, 0, uint.MaxValue, null);
            }, true);
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

            ActivateJoinLobbyControls();
        }

        private void ActivateLobbyControls()
        {
            _mainThreadAccessor.MainThreadQueue.Enqueue(() =>
            {
                ReadyButtonObject.GetComponentInChildren<TextMeshProUGUI>().SetText(StringConsts.MultiplayerWindow.HostMenu.ReadyNotReadyButtonLabel);

                JoinLobbyControlsObject.SetActive(false);
                LobbyControls.SetActive(true);
                LobbyWindow.SetActive(true);
            });
        }

        private void ActivateJoinLobbyControls()
        {
            _mainThreadAccessor.MainThreadQueue.Enqueue(() =>
            {
                JoinLobbyControlsObject.SetActive(true);
                LobbyControls.SetActive(false);
                LobbyWindow.SetActive(false);
            });
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

            SetButtonActive(JoinButtonObject, false);
        }

        private void SetButtonActive(GameObject button, bool isActive)
        {
            _mainThreadAccessor.MainThreadQueue.Enqueue(() =>
            {
                button.GetComponent<OwlcatButton>().Interactable = isActive;
            });
        }

        public override void Deactivate()
        {
            ActivateJoinLobbyControls();
            Lobby.Reset();

            base.Deactivate();
        }

        protected override ModalActionConfirmation GetDeactivationConfirmationInternal()
        {
            if (_multiplayerClient.IsActive)
            {
                return new ModalActionConfirmation
                {
                    Text = StringConsts.MultiplayerWindow.JoinMenu.LeaveGameMessage
                };
            }
            else if (_multiplayerClient.IsConnecting)
            {
                return new ModalActionConfirmation
                {
                    Text = StringConsts.MultiplayerWindow.JoinMenu.LeaveWhileConnectingMessage,
                    ModalType = MessageModalBase.ModalType.Message
                };
            }

            return base.GetDeactivationConfirmationInternal();
        }
    }
}
