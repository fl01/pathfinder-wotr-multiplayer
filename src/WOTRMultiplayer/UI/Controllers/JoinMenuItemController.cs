using System.Collections.Generic;
using Kingmaker.Localization;
using Kingmaker.PubSubSystem;
using Kingmaker.UI;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.Controls.Button;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WOTRMultiplayer.Abstractions;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Controllers.Menu;
using WOTRMultiplayer.Abstractions.Unity;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.UI.Menu;

namespace WOTRMultiplayer.UI.Controllers
{
    public class JoinMenuItemController : MenuItemControllerBase, IJoinMenuItemController
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

        protected override GameObject ReadyButtonObject => LobbyControls.transform
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
            IResourceProvider resourceProvider,
            IUIFactory uIFactory)
            : base(logger, lobbyWindowController, mainThreadAccessor, resourceProvider, multiplayerClient)
        {
            _logger = logger;
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

            ActivateJoinLobbyControls();

            SetupHandlers(true);
            Lobby.SetActiveOwner(LobbyWindowOwner.JoinMenu);
            base.Activate();
        }

        public override void Deactivate()
        {
            if (!IsActive)
            {
                return;
            }

            if (_multiplayerClient.IsInLobby)
            {
                _multiplayerClient.Reset();
            }

            SetupHandlers(false);

            base.Deactivate();
        }

        protected override ModalActionConfirmation GetDeactivationConfirmationInternal()
        {
            if (_multiplayerClient.IsInLobby)
            {
                return new ModalActionConfirmation
                {
                    MessageKey = WellKnownKeys.MultiplayerWindow.JoinMenu.Deactivation.Connected.Key
                };
            }
            else if (_multiplayerClient.IsConnecting)
            {
                return new ModalActionConfirmation
                {
                    MessageKey = WellKnownKeys.MultiplayerWindow.JoinMenu.Deactivation.Connecting.Key,
                    ModalType = MessageModalBase.ModalType.Message
                };
            }

            return base.GetDeactivationConfirmationInternal();
        }

        protected override void InitializeInternal(GameObject baseLayout)
        {
            var label = MenuItem.GetComponentInChildren<TextMeshProUGUI>();
            label.SetText(new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.JoinMenu.Title.Key });

            _menuContent = Object.Instantiate(baseLayout, baseLayout.transform);
            _menuContent.name = JoinMenuItemContentObjectName;
            _menuContent.AddComponent<VerticalLayoutGroup>().padding = new RectOffset(0, 0, 25, 0);
            _menuContent.CleanupAllChildren();
            _menuContent.SetActive(false);

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
            serverPlaceholderInput.SetText(new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.JoinMenu.ServerAddress.Placeholder.Key });
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
            joinLobbyButtonObject.GetComponentInChildren<TextMeshProUGUI>().SetText(new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.JoinMenu.JoinButton.Key });
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
            leaveButtonObject.GetComponentInChildren<TextMeshProUGUI>().SetText(new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.JoinMenu.LeaveButton.Key });
            var leaveButton = leaveButtonObject.GetComponent<OwlcatButton>();
            leaveButton.OnLeftClick.AddListener(OnLeaveButtonClicked);
        }

        private void OnMultiplayerCharacterOwnerChanged(int characterIndex, int playerIndex)
        {
            _logger.LogInformation("Updating character owner. CharacterIndex={CharacterIndex}, PlayerIndex={PlayerIndex}", characterIndex, playerIndex);
            Lobby.UpdateCharacterOwnerDropdown(characterIndex, playerIndex);
        }

        private void SetupHandlers(bool enable)
        {
            _multiplayerClient.OnNetworkError = enable ? OnMultiplayerError : null;
            _multiplayerClient.OnConnected = enable ? OnMultiplayerConnected : null;
            _multiplayerClient.OnPlayersChanged = enable ? OnMultiplayerPlayersChanged : null;
            _multiplayerClient.OnGameCharactersChanged = enable ? OnMultiplayerGameCharactersChanged : null;
            _multiplayerClient.OnCharacterOwnerChanged = enable ? OnMultiplayerCharacterOwnerChanged : null;
            _multiplayerClient.OnNewGameSequenceStarted = enable ? OnMultiplayerNewGameSequenceStarted : null;
        }

        protected override void DisposeInternal()
        {
            SetupHandlers(false);
            base.DisposeInternal();
        }

        private void OnMultiplayerConnected(NetworkGameConnectivity connectivity)
        {
            Lobby.UpdateServerInfo(connectivity);
            SetButtonActive(JoinButtonObject, true);
            ActivateLobbyControls();
        }

        private void OnMultiplayerPlayersChanged(List<NetworkPlayer> players)
        {
            Lobby.UpdatePlayers(players);

            MainThreadAccessor.Post(() =>
            {
                ReadyButtonObject.GetComponent<OwlcatButton>().Interactable = true;
                LeaveButtonObject.GetComponent<OwlcatButton>().Interactable = true;
            });
        }

        private void OnMultiplayerGameCharactersChanged(List<NetworkCharacter> characters)
        {
            Lobby.UpdateCharacters(characters, false);
        }

        private void OnMultiplayerError()
        {
            ActivateJoinLobbyControls();
        }

        private void OnLeaveButtonClicked()
        {
            _logger.LogInformation("Leave button clicked");
            ActivateJoinLobbyControls();
        }

        private void ActivateLobbyControls()
        {
            MainThreadAccessor.Post(() =>
            {
                ToggleReadyButton(false);

                JoinLobbyControlsObject.SetActive(false);
                LobbyControls.SetActive(true);
                LobbyWindow.SetActive(true);

                ReadyButtonObject.GetComponent<OwlcatButton>().Interactable = false;
                LeaveButtonObject.GetComponent<OwlcatButton>().Interactable = false;
            });
        }

        private void ActivateJoinLobbyControls()
        {
            _multiplayerClient.Reset();

            MainThreadAccessor.Post(() =>
            {
                Lobby.ResetData();
                SetButtonActive(JoinButtonObject, true);
                JoinLobbyControlsObject.SetActive(true);
                LobbyControls.SetActive(false);
                LobbyWindow.SetActive(false);
            });
        }

        private void OnJoinButtonClicked()
        {
            _logger.LogInformation("Join button clicked");
            var address = ServerAddressObject.GetComponent<TMP_InputField>().text.Trim();
            var result = _multiplayerClient.Connect(address);
            if (!result.IsOk)
            {
                var message = new LocalizedString { Key = result.MessageKey };
                EventBus.RaiseEvent<IMessageModalUIHandler>(x => x.HandleOpen(message, MessageModalBase.ModalType.Message));
                return;
            }

            SetButtonActive(JoinButtonObject, false);
        }

        private void SetButtonActive(GameObject button, bool isActive)
        {
            MainThreadAccessor.Post(() =>
            {
                button.GetComponent<OwlcatButton>().Interactable = isActive;
            });
        }
    }
}
