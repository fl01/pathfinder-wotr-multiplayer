using System.Collections.Generic;
using Kingmaker.PubSubSystem;
using Kingmaker.UI;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.Controls.Button;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.MP.Actors;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Controllers.Menu;
using WOTRMultiplayer.Abstractions.Unity;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.MP.Entities;
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
            IUIFactory uIFactory,
            IGameInteractionService gameInteractionService)
            : base(logger, lobbyWindowController, mainThreadAccessor, gameInteractionService)
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
                _multiplayerClient.Dispose();
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
                    Text = UIStringConsts.MultiplayerWindow.JoinMenu.LeaveGameMessage
                };
            }
            else if (_multiplayerClient.IsConnecting)
            {
                return new ModalActionConfirmation
                {
                    Text = UIStringConsts.MultiplayerWindow.JoinMenu.LeaveWhileConnectingMessage,
                    ModalType = MessageModalBase.ModalType.Message
                };
            }

            return base.GetDeactivationConfirmationInternal();
        }

        protected override void InitializeInternal(GameObject baseLayout)
        {
            var label = MenuItem.GetComponentInChildren<TextMeshProUGUI>();
            label.SetText(UIStringConsts.MultiplayerWindow.JoinMenuLabel);

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
            Lobby.InitializeContent(LobbyWindowOwner.JoinMenu, lobbyWindow.transform, false);

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
            serverPlaceholderInput.SetText(UIStringConsts.MultiplayerWindow.JoinMenu.ServerInputPlaceholder);
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
            joinLobbyButtonObject.GetComponentInChildren<TextMeshProUGUI>().SetText(UIStringConsts.MultiplayerWindow.JoinMenu.JoinButtonLabel);
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
            leaveButtonObject.GetComponentInChildren<TextMeshProUGUI>().SetText(UIStringConsts.MultiplayerWindow.JoinMenu.LeaveButtonLabel);
            var leaveButton = leaveButtonObject.GetComponent<OwlcatButton>();
            leaveButton.OnLeftClick.AddListener(OnLeaveButtonClicked);
        }

        private void OnMultiplayerCharacterOwnerChanged(int characterIndex, int playerIndex)
        {
            _logger.LogInformation("Updating character owner. CharacterIndex={characterIndex}, PlayerIndex={playerIndex}", characterIndex, playerIndex);
            Lobby.UpdateCharacterOwnerDropdown(characterIndex, playerIndex);
        }

        private void SetupHandlers(bool enable)
        {
            _multiplayerClient.OnNetworkError = enable ? OnMultiplayerError : null;
            _multiplayerClient.OnConnected = enable ? OnMultiplayerConnected : null;
            _multiplayerClient.OnPlayersChanged = enable ? OnMultiplayerPlayersChanged : null;
            _multiplayerClient.OnGameCharactersChanged = enable ? OnMultiplayerGameCharactersChanged : null;
            _multiplayerClient.OnCharacterOwnerChanged = enable ? OnMultiplayerCharacterOwnerChanged : null;
            _multiplayerClient.OnStartGame = enable ? OnMultiplayerStartGame : null;
        }

        protected override void DisposeInternal()
        {
            SetupHandlers(false);
            base.DisposeInternal();
        }

        private void OnMultiplayerStartGame(string saveFilePath)
        {
            _logger.LogInformation("Starting multiplayer game as a client");
            GameInteraction.LoadGameFromMainMenu(saveFilePath);
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
        }

        private void OnMultiplayerGameCharactersChanged(List<NetworkCharacterOwnership> characters)
        {
            Lobby.UpdateCharacters(characters);
        }

        private void OnMultiplayerError(string errorMessage)
        {
            _logger.LogError("Multiplayer client error. ErrorText={errorMessage}", errorMessage);
            ActivateJoinLobbyControls();
        }

        private void OnReadyButtonClicked()
        {
            var isReady = _multiplayerClient.ReadyChanged();
            var label = isReady ? UIStringConsts.MultiplayerWindow.HostMenu.ReadyButtonLabel
                : UIStringConsts.MultiplayerWindow.HostMenu.ReadyNotReadyButtonLabel;
            ReadyButtonObject.GetComponentInChildren<TextMeshProUGUI>().SetText(label);
        }

        private void OnLeaveButtonClicked()
        {
            _multiplayerClient.Dispose();

            ActivateJoinLobbyControls();
        }

        private void ActivateLobbyControls()
        {
            MainThreadAccessor.Enqueue(() =>
            {
                ReadyButtonObject.GetComponentInChildren<TextMeshProUGUI>().SetText(UIStringConsts.MultiplayerWindow.HostMenu.ReadyNotReadyButtonLabel);

                JoinLobbyControlsObject.SetActive(false);
                LobbyControls.SetActive(true);
                LobbyWindow.SetActive(true);
            });
        }

        private void ActivateJoinLobbyControls()
        {
            MainThreadAccessor.Enqueue(() =>
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
            var rawAddress = ServerAddressObject.transform.Find(UIFactory.InputLabelObjectName).GetComponent<TextMeshProUGUI>().text;
            // thank you for zero-width space
            var address = rawAddress.Trim('\u200B').Trim();
            var result = _multiplayerClient.Connect(address);
            if (!result.IsOk)
            {
                EventBus.RaiseEvent<IMessageModalUIHandler>(window =>
                {
                    window.HandleOpen(result.Message, MessageModalBase.ModalType.Message);
                });
                return;
            }

            SetButtonActive(JoinButtonObject, false);
        }

        private void SetButtonActive(GameObject button, bool isActive)
        {
            MainThreadAccessor.Enqueue(() =>
            {
                button.GetComponent<OwlcatButton>().Interactable = isActive;
            });
        }
    }
}
