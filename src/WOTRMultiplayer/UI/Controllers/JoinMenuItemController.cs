using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DG.Tweening;
using Kingmaker.Localization;
using Kingmaker.UI;
using Kingmaker.UI.Common;
using Kingmaker.Utility;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Owlcat.Runtime.UI.Controls.Button;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WOTRMultiplayer.Abstractions;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.Settings;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Controllers.Menu;
using WOTRMultiplayer.Abstractions.Unity;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.UI.Windows;

namespace WOTRMultiplayer.UI.Controllers
{
    public class JoinMenuItemController : MenuItemControllerBase, IJoinMenuItemController
    {
        public const string RootContentScreenObjectName = "RootContentScreen";
        public const string JoinMenuItemContentObjectName = "JoinMenuItemContent";
        public const string JoinLobbyControlsMenuObjectName = "JoinLobbyControlsMenu";
        public const string ServerAddressInputObjectName = "ServerAddressInput";
        public const string DirectJoinServerButtonObjectName = "DirectJoinServerButton";
        public const string GameCodeJoinServerButtonObjectName = "GameCodeJoinServerButton";
        public const string GameCodeInputObjectName = "GameCodeInputObject";
        public const string GamePasswordObjectName = "GamePasswordInputObject";

        public const string LobbyControlsMenuObjectName = "LobbyControlsMenu";
        public const string LobbyControlsMenuReadyButtonObjectName = "ReadyButton";
        public const string LobbyControlsMenuLeaveButtonObjectName = "LeaveButton";

        public const string ConnectionControlsObjectName = "LobbyConnectionControls";
        public const string ConnectionMethodObjectName = "ConnectionMethodObject";
        public const string DirectConnectionObjectName = "LobbyDirectIPConnection";
        public const string GameCodeConnectionObjectName = "LobbyGameCodeConnection";

        public const string ServerHistoryObjectName = "ServerHistory";
        public const string ServerHistoryHeaderObjectName = "ServerHistoryHeader";
        public const string ServerHistoryRecordsObjectName = "ServerHistoryRecords";
        public const string ServerHistoryRecordsBorderObjectName = "ServerHistoryRecordsBorder";

        public const string LobbyWindowObjectName = "LobbyWindow";

        public const string GameTitleObjectName = "LobbyTitleObject";

        private string ConnectionHistoryFilePath => Path.Combine(GameInteractionService.GetPersistentDataPath(), "connections.json");

        private readonly ILogger<JoinMenuItemController> _logger;
        private readonly IMultiplayerClient _multiplayerClient;
        private readonly IMultiplayerSettingsService _multiplayerSettingsService;
        private readonly IIPEndPointParser _ipEndPointParser;
        private readonly IPlayerNotificationService _playerNotificationService;

        private HashSet<ConnectionHistoryRecord> _connectionHistory;
        private HashSet<ConnectionHistoryRecord> ConnectionHistory
        {
            get
            {
                _connectionHistory ??= LoadConnectionHistory();
                return _connectionHistory;
            }
        }

        private GameObject _menuContent;

        protected override GameObject MenuContent => _menuContent;
        protected override LobbyWindowOwner Owner => LobbyWindowOwner.JoinMenu;

        protected GameObject LobbyTitleGameObject => _menuContent.transform
            .Find(RootContentScreenObjectName)
            .Find(GameTitleObjectName)
            .gameObject;

        protected TextMeshProUGUI LobbyTitle => LobbyTitleGameObject
            .GetComponent<TextMeshProUGUI>();

        protected GameObject JoinLobbyControlsObject => _menuContent.transform
            .Find(RootContentScreenObjectName)
            .Find(JoinLobbyControlsMenuObjectName)
            .gameObject;

        protected GameObject ServerHistoryRecords => DirectConnectionObject.transform
            .Find(ServerHistoryObjectName)
            .Find(ServerHistoryRecordsObjectName)
            .gameObject;

        protected TMP_Dropdown ConnectionMethodDropdown => JoinLobbyControlsObject.transform
            .Find(ConnectionControlsObjectName)
            .Find(ConnectionMethodObjectName)
            .GetComponentInChildren<TMP_Dropdown>();

        protected GameObject GameCodeInputObject => JoinLobbyControlsObject.transform
            .Find(GameCodeConnectionObjectName)
            .Find(GameCodeInputObjectName)
            .gameObject;

        protected GameObject GamePasswordInputObject => JoinLobbyControlsObject.transform
            .Find(GameCodeConnectionObjectName)
            .Find(GamePasswordObjectName)
            .gameObject;

        protected GameObject DirectConnectionObject => JoinLobbyControlsObject.transform
            .Find(DirectConnectionObjectName)
            .gameObject;

        protected GameObject GameCodeConnectionObject => JoinLobbyControlsObject.transform
            .Find(GameCodeConnectionObjectName)
            .gameObject;

        protected GameObject DirectConnectJoinButtonObject => DirectConnectionObject.transform
            .Find(DirectJoinServerButtonObjectName)
            .gameObject;

        protected GameObject GameCodeConnectJoinButtonObject => GameCodeConnectionObject.transform
            .Find(GameCodeJoinServerButtonObjectName)
            .gameObject;

        protected GameObject ServerAddressObject => DirectConnectionObject.transform
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
            IMultiplayerSettingsService multiplayerSettingsService,
            IIPEndPointParser ipEndPointParser,
            IFileSystemService fileSystemService,
            IUIFactory uiFactory,
            IGameInteractionService gameInteractionService,
            IPlayerNotificationService playerNotificationService)
            : base(logger, lobbyWindowController, mainThreadAccessor, resourceProvider, fileSystemService, uiFactory, gameInteractionService, multiplayerClient)
        {
            _logger = logger;
            _multiplayerClient = multiplayerClient;
            _multiplayerSettingsService = multiplayerSettingsService;
            _ipEndPointParser = ipEndPointParser;
            _playerNotificationService = playerNotificationService;
        }

        public override void Activate()
        {
            _logger.LogInformation("Trying to activate");

            if (IsActive)
            {
                return;
            }

            var settings = _multiplayerSettingsService.GetSettings();
            var inputContentType = settings.HideServerAddress ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            var inputField = ServerAddressObject.GetComponent<TMP_InputField>();
            inputField.text = string.Empty;
            inputField.contentType = inputContentType;

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

            _menuContent = UnityEngine.Object.Instantiate(baseLayout, baseLayout.transform);
            _menuContent.name = JoinMenuItemContentObjectName;
            _menuContent.AddComponent<VerticalLayoutGroup>().padding = new RectOffset(0, 0, 25, 0);
            _menuContent.CleanupAllChildren();
            _menuContent.SetActive(false);

            var menuContentRect = _menuContent.GetComponent<RectTransform>();
            menuContentRect.sizeDelta = new Vector2(menuContentRect.sizeDelta.x * 0.4f, menuContentRect.sizeDelta.y * 0.88f);

            var content = UIFactory.CreateDefaultGameObject(_menuContent.transform);
            content.name = RootContentScreenObjectName;
            content.AddComponent<VerticalLayoutGroup>();
            var gameTitle = UIFactory.CreateDefaultGameObject(content.transform);
            gameTitle.name = GameTitleObjectName;
            var title = gameTitle.AddComponent<TextMeshProUGUI>();
            title.alignment = TextAlignmentOptions.Center;
            title.fontSize = 28;
            title.material = UIFactory.DefaultTextMesh.Material;
            title.color = UIFactory.DefaultTextMesh.Color;
            var gameTitleVertical = gameTitle.AddComponent<VerticalLayoutGroup>();
            gameTitleVertical.padding = new RectOffset(0, 0, 0, 55);

            var lobbyWindow = UIFactory.CreateDefaultGameObject(content.transform);
            lobbyWindow.name = LobbyWindowObjectName;
            var lobbyWindowLayout = lobbyWindow.AddComponent<LayoutElement>();
            lobbyWindowLayout.preferredHeight = menuContentRect.sizeDelta.y;
            var lobbyWindowVertical = lobbyWindow.AddComponent<VerticalLayoutGroup>();
            lobbyWindowVertical.padding = new RectOffset(0, 0, 0, 20);
            var lobbyWindowRect = lobbyWindow.GetComponent<RectTransform>();
            lobbyWindowRect.sizeDelta = menuContentRect.sizeDelta;
            Lobby.InitializeContent(LobbyWindowOwner.JoinMenu, lobbyWindow.transform);

            InitializeJoinLobbyControls(content.transform, menuContentRect.sizeDelta);

            // leave + ready buttons?
            var lobbyControlsMenu = UIFactory.CreateDefaultGameObject(content.transform);
            lobbyControlsMenu.name = LobbyControlsMenuObjectName;
            lobbyControlsMenu.AddComponent<HorizontalLayoutGroup>();
            lobbyControlsMenu.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
            var lobbyControlsMenuLayout = lobbyControlsMenu.AddComponent<LayoutElement>();
            lobbyControlsMenuLayout.preferredHeight = menuContentRect.sizeDelta.y * 0.07f;

            var readyButtonObject = UIFactory.CreateButton(lobbyControlsMenu.transform);
            readyButtonObject.name = LobbyControlsMenuReadyButtonObjectName;
            var readyButtonObjectLayout = readyButtonObject.AddComponent<LayoutElement>();
            readyButtonObjectLayout.preferredWidth = menuContentRect.sizeDelta.x * 0.2f;
            readyButtonObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var readyButton = readyButtonObject.GetComponent<OwlcatButton>();
            readyButton.OnLeftClick.AddListener(OnReadyButtonClicked);

            var leaveButtonObject = UIFactory.CreateButton(lobbyControlsMenu.transform);
            leaveButtonObject.name = LobbyControlsMenuLeaveButtonObjectName;
            var leaveButtonObjectLayout = leaveButtonObject.AddComponent<LayoutElement>();
            leaveButtonObjectLayout.preferredWidth = menuContentRect.sizeDelta.x * 0.2f;
            leaveButtonObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            leaveButtonObject.GetComponentInChildren<TextMeshProUGUI>().SetText(new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.JoinMenu.LeaveButton.Key });
            var leaveButton = leaveButtonObject.GetComponent<OwlcatButton>();
            leaveButton.OnLeftClick.AddListener(OnLeaveButtonClicked);
        }

        private void InitializeJoinLobbyControls(Transform parent, Vector2 fullSize)
        {
            var joinLobbyControlsMenu = UIFactory.CreateDefaultGameObject(parent);
            joinLobbyControlsMenu.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            joinLobbyControlsMenu.name = JoinLobbyControlsMenuObjectName;
            joinLobbyControlsMenu.AddComponent<VerticalLayoutGroup>();
            joinLobbyControlsMenu.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var connectionControlsObject = UIFactory.CreateDefaultGameObject(joinLobbyControlsMenu.transform);
            connectionControlsObject.AddComponent<VerticalLayoutGroup>();
            connectionControlsObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            connectionControlsObject.name = ConnectionControlsObjectName;
            var connectionControlsObjectLayout = connectionControlsObject.AddComponent<LayoutElement>();
            connectionControlsObjectLayout.preferredHeight = 150;
            CreateConnectionControls(connectionControlsObject.transform, fullSize);

            var directConnectionObject = UIFactory.CreateDefaultGameObject(joinLobbyControlsMenu.transform);
            directConnectionObject.name = DirectConnectionObjectName;
            directConnectionObject.AddComponent<VerticalLayoutGroup>().spacing = 10f;
            directConnectionObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            CreateDirectIPConnectionControls(directConnectionObject.transform, fullSize);
            directConnectionObject.SetActive(false);

            var gameConnectionObject = UIFactory.CreateDefaultGameObject(joinLobbyControlsMenu.transform);
            gameConnectionObject.name = GameCodeConnectionObjectName;
            gameConnectionObject.AddComponent<VerticalLayoutGroup>().spacing = 10f;
            gameConnectionObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            gameConnectionObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            CreateGameCodeConnectionControls(gameConnectionObject.transform, fullSize);
            gameConnectionObject.SetActive(false);

            ConnectionMethodDropdown.onValueChanged.Invoke(0);
        }

        private void CreateGameCodeConnectionControls(Transform parent, Vector2 fullSize)
        {
            var gameCodeInputObject = UIFactory.CreateInput(parent.transform);
            gameCodeInputObject.name = GameCodeInputObjectName;
            var gameCodeInputObjectLayout = gameCodeInputObject.AddComponent<LayoutElement>();
            gameCodeInputObjectLayout.preferredWidth = fullSize.x * 0.65f;
            gameCodeInputObjectLayout.preferredHeight = 35;
            var gameCodePlaceholder = gameCodeInputObject.transform.Find(UI.UIFactory.InputPlaceholderObjectName);
            var gameCodePlaceholderInput = gameCodePlaceholder.GetComponent<TextMeshProUGUI>();
            gameCodePlaceholderInput.SetText(new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.JoinMenu.GameCodeConnection.GameCode.Placeholder.Key });
            gameCodePlaceholderInput.alignment = TextAlignmentOptions.Center;
            var gameCodeInputLabelObject = gameCodeInputObject.transform.Find(UI.UIFactory.InputLabelObjectName);
            var gameCodeInput = gameCodeInputLabelObject.GetComponent<TextMeshProUGUI>();
            gameCodeInput.overflowMode = TextOverflowModes.Truncate;
            gameCodeInput.alignment = TextAlignmentOptions.Center;

            var passwordInputObject = UIFactory.CreateInput(parent.transform);
            passwordInputObject.name = GamePasswordObjectName;
            var passwordInputObjectLayout = passwordInputObject.AddComponent<LayoutElement>();
            passwordInputObjectLayout.preferredWidth = fullSize.x * 0.65f;
            passwordInputObjectLayout.preferredHeight = 35;
            var passwordPlaceholder = passwordInputObject.transform.Find(UI.UIFactory.InputPlaceholderObjectName);
            var passwordPlaceholderInput = passwordPlaceholder.GetComponent<TextMeshProUGUI>();
            passwordPlaceholderInput.SetText(new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.JoinMenu.GameCodeConnection.Password.Placeholder.Key });
            passwordPlaceholderInput.alignment = TextAlignmentOptions.Center;
            var passwordInputLabelObject = passwordInputObject.transform.Find(UI.UIFactory.InputLabelObjectName);
            var passwordInput = passwordInputLabelObject.GetComponent<TextMeshProUGUI>();
            passwordInput.overflowMode = TextOverflowModes.Truncate;
            passwordInput.alignment = TextAlignmentOptions.Center;
            var password = passwordInputObject.GetComponent<TMP_InputField>();
            password.text = string.Empty;
            password.contentType = TMP_InputField.ContentType.Password;

            var joinLobbyButtonObject = UIFactory.CreateButton(parent.transform);
            joinLobbyButtonObject.name = GameCodeJoinServerButtonObjectName;
            var joinLobbyButtonObjectLayout = joinLobbyButtonObject.AddComponent<LayoutElement>();
            joinLobbyButtonObjectLayout.preferredWidth = fullSize.x * 0.35f;
            joinLobbyButtonObjectLayout.preferredHeight = 50;
            joinLobbyButtonObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var button = joinLobbyButtonObject.GetComponent<OwlcatButton>();
            button.OnLeftClick.AddListener(OnGameCodeConnectJoinButtonClicked);
        }

        private void CreateDirectIPConnectionControls(Transform parent, Vector2 fullSize)
        {
            var serverInfoInputObject = UIFactory.CreateInput(parent.transform);
            serverInfoInputObject.name = ServerAddressInputObjectName;
            var serverInfoInputObjectLayout = serverInfoInputObject.AddComponent<LayoutElement>();
            serverInfoInputObjectLayout.preferredHeight = 35;
            var serverPlaceholder = serverInfoInputObject.transform.Find(UI.UIFactory.InputPlaceholderObjectName);
            var serverPlaceholderInput = serverPlaceholder.GetComponent<TextMeshProUGUI>();
            serverPlaceholderInput.SetText(new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.JoinMenu.DirectConnection.ServerAddress.Placeholder.Key });
            serverPlaceholderInput.alignment = TextAlignmentOptions.Center;
            var serverInfoInputLabelObject = serverInfoInputObject.transform.Find(UI.UIFactory.InputLabelObjectName);
            var serverInfoInput = serverInfoInputLabelObject.GetComponent<TextMeshProUGUI>();
            serverInfoInput.overflowMode = TextOverflowModes.Truncate;
            serverInfoInput.alignment = TextAlignmentOptions.Center;

            var joinLobbyButtonObject = UIFactory.CreateButton(parent.transform);
            joinLobbyButtonObject.name = DirectJoinServerButtonObjectName;
            var joinLobbyButtonObjectLayout = joinLobbyButtonObject.AddComponent<LayoutElement>();
            joinLobbyButtonObjectLayout.preferredWidth = fullSize.x * 0.35f;
            joinLobbyButtonObjectLayout.preferredHeight = 50;
            joinLobbyButtonObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var button = joinLobbyButtonObject.GetComponent<OwlcatButton>();
            button.OnLeftClick.AddListener(OnDirectConnectJoinButtonClicked);

            var serverHistoryContainer = UIFactory.CreateDefaultGameObject(parent.transform);
            serverHistoryContainer.AddComponent<VerticalLayoutGroup>();
            serverHistoryContainer.name = ServerHistoryObjectName;
            var serverHistoryHeaderObject = UIFactory.CreateDefaultGameObject(serverHistoryContainer.transform);
            serverHistoryHeaderObject.name = ServerHistoryHeaderObjectName;
            serverHistoryHeaderObject.AddComponent<VerticalLayoutGroup>().padding = new RectOffset(0, 0, 15, 35); ;
            var serverHistoryHeader = serverHistoryHeaderObject.AddComponent<TextMeshProUGUI>();
            var headerText = UIUtility.GetSaberBookFormat(new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.JoinMenu.DirectConnection.ServerHistory.Header.Key });
            serverHistoryHeader.SetText(headerText);
            serverHistoryHeader.fontSize = 28;
            serverHistoryHeader.horizontalAlignment = HorizontalAlignmentOptions.Center;
            serverHistoryHeader.material = UIFactory.DefaultTextMesh.Material;
            serverHistoryHeader.color = UIFactory.DefaultTextMesh.Color;
            var serverHistoryRecordsObject = UIFactory.CreateDefaultGameObject(serverHistoryContainer.transform);
            serverHistoryRecordsObject.name = ServerHistoryRecordsObjectName;
            serverHistoryRecordsObject.AddComponent<VerticalLayoutGroup>();
            var serverHistoryRecordsBorder = UIFactory.CreateBorderDecoration(serverHistoryRecordsObject.transform);
            serverHistoryRecordsBorder.name = ServerHistoryRecordsBorderObjectName;
            var serverHistoryRecordsLayoutGroup = serverHistoryRecordsObject.AddComponent<VerticalLayoutGroup>();
        }

        private void CreateConnectionControls(Transform parent, Vector2 fullSize)
        {
            var labelObject = UIFactory.CreateDefaultGameObject(parent.transform);
            labelObject.name = ConnectionMethodObjectName;
            labelObject.AddComponent<VerticalLayoutGroup>();
            labelObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var textBoxObject = UIFactory.CreateDefaultGameObject(labelObject.transform);
            var textBox = textBoxObject.AddComponent<TextMeshProUGUI>();
            textBox.verticalAlignment = VerticalAlignmentOptions.Middle;
            textBox.alignment = TextAlignmentOptions.Center;
            textBox.horizontalAlignment = HorizontalAlignmentOptions.Center;
            textBox.SetText(new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.JoinMenu.ConnectionControls.Title.Key });
            textBox.margin = new Vector4(0f, 0f, 0f, 25f);
            textBox.material = UIFactory.DefaultTextMesh.Material;
            textBox.color = UIFactory.DefaultTextMesh.Color;
            var dropdownContainerObject = Main.Multiplayer.UIFactory.CreateDropdown(fullSize.x * 0.6f, labelObject.transform);
            var dropdownObject = dropdownContainerObject.transform.Find(UI.UIFactory.DropdownGameObjectName);
            dropdownObject.GetComponent<RectTransform>().Centered();
            var tmpDropdown = dropdownObject.GetComponent<TMP_Dropdown>();
            tmpDropdown.ClearOptions();
            var options = new List<TMP_Dropdown.OptionData>
            {
                new ConnectionMethodOptionData( ConnectionMethodType.DirectIP, new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.JoinMenu.ConnectionControls.Options.DirectConnect.Key }),
                new ConnectionMethodOptionData( ConnectionMethodType.GameCode, new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.JoinMenu.ConnectionControls.Options.GameCodeConnect.Key }),

            };
            tmpDropdown.AddOptions(options);
            tmpDropdown.onValueChanged.AddListener(value =>
            {
                var option = tmpDropdown.options[value] as ConnectionMethodOptionData;
                switch (option.Method)
                {
                    case ConnectionMethodType.DirectIP:
                        DirectConnectionObject.SetActive(true);
                        GameCodeConnectionObject.SetActive(false);
                        break;
                    case ConnectionMethodType.GameCode:
                        DirectConnectionObject.SetActive(false);
                        GameCodeConnectionObject.SetActive(true);
                        break;
                }
            });
        }

        private void OnMultiplayerCharacterOwnerChanged(NetworkCharacter character)
        {
            _logger.LogInformation("Updating character owner. CharacterName={CharacterName}, CharacterId={CharacterId}, OwnerId={OwnerId}", character.Name, character.UnitId, character.Owner.Id);
            Lobby.UpdateCharacterOwnerDropdown(character);
        }

        private void SetupHandlers(bool enable)
        {
            _multiplayerClient.OnNetworkError = enable ? OnMultiplayerError : null;
            _multiplayerClient.OnConnected = enable ? OnMultiplayerConnected : null;
            _multiplayerClient.OnPlayersChanged = enable ? OnMultiplayerPlayersChanged : null;
            _multiplayerClient.OnCharactersChanged = enable ? OnMultiplayerCharactersChanged : null;
            _multiplayerClient.OnCharacterOwnerChanged = enable ? OnMultiplayerCharacterOwnerChanged : null;
            _multiplayerClient.OnNewGameSequenceStarted = enable ? OnMultiplayerNewGameSequenceStarted : null;
            _multiplayerClient.OnSaveGameTransferProgressChanged = enable ? OnMultiplayerSaveGameTransferProgressChanged : null;
            _multiplayerClient.OnGameStarted = enable ? OnMultiplayerOnGameStarted : null;
        }

        protected override void DisposeInternal()
        {
            SetupHandlers(false);

            base.DisposeInternal();
        }

        private void OnMultiplayerConnected(GameConnectivity connectivity)
        {
            if (connectivity.Endpoint != null)
            {
                AddSuccessfulConnectionRecord(connectivity);
            }

            Lobby.UpdateServerInfo(connectivity);
            ActivateLobbyControls();
        }

        private void OnMultiplayerPlayersChanged(NetworkLobbyStage lobbyStage, List<NetworkPlayer> players)
        {
            Lobby.UpdatePlayers(players);

            MainThreadAccessor.Post(() =>
            {
                ReadyButtonObject.GetComponent<OwlcatButton>().Interactable = true;
                LeaveButtonObject.GetComponent<OwlcatButton>().Interactable = true;
            });
        }

        private void OnMultiplayerCharactersChanged(string title, List<NetworkCharacter> characters)
        {
            MainThreadAccessor.Post(() =>
            {
                var titleText = UIUtility.GetSaberBookFormat(title);
                LobbyTitle.SetText(titleText);
            });

            Lobby.UpdateCharacters(characters, isDropdownInteractable: false);
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
                SetJoiningState(false, null);

                LobbyTitle.SetText(string.Empty);
                LobbyTitleGameObject.SetActive(true);

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
                LobbyTitle.SetText(string.Empty);
                LobbyTitleGameObject.SetActive(false);
                Lobby.ResetData();
                SetJoiningState(false, null);
                JoinLobbyControlsObject.SetActive(true);
                LobbyControls.SetActive(false);
                LobbyWindow.SetActive(false);

                UpdateServerHistory();
            });
        }

        private void UpdateServerHistory()
        {
            ServerHistoryRecords.GetComponentsInChildren<OwlcatButton>().ForEach(x => x.m_OnLeftClick.RemoveAllListeners());
            ServerHistoryRecords.CleanupAllChildren(x => x.name != ServerHistoryRecordsBorderObjectName);

            var settings = _multiplayerSettingsService.GetSettings();
            foreach (var record in ConnectionHistory.OrderByDescending(x => x.JoinedAt))
            {
                var recordObject = UIFactory.CreateDefaultGameObject(ServerHistoryRecords.transform);
                recordObject.AddComponent<HorizontalLayoutGroup>();
                recordObject.AddComponent<ContentSizeFitter>();
                recordObject.AddComponent<LayoutElement>().preferredHeight = 50;

                var serverAddressObject = UIFactory.CreateDefaultGameObject(recordObject.transform);
                var serverAddressRect = serverAddressObject.GetComponent<RectTransform>();
                serverAddressRect.pivot = Vector2.zero;
                serverAddressObject.AddComponent<ContentSizeFitter>();
                serverAddressObject.AddComponent<LayoutElement>().preferredWidth = 250;
                var serverAddressText = serverAddressObject.AddComponent<TextMeshProUGUI>();
                serverAddressText.material = UIFactory.DefaultTextMesh.Material;
                serverAddressText.color = UIFactory.DefaultTextMesh.Color;
                serverAddressText.alignment = TextAlignmentOptions.MidlineLeft;
                serverAddressText.horizontalAlignment = HorizontalAlignmentOptions.Left;
                var serverAddress = settings.HideServerAddress ? "***.***.***.***:****" : record.Address;
                serverAddressText.SetText(serverAddress);

                var lastJoinedObject = UIFactory.CreateDefaultGameObject(recordObject.transform);
                var lastJoinedRect = lastJoinedObject.GetComponent<RectTransform>();
                lastJoinedRect.pivot = Vector2.zero;
                var lastJoinedText = lastJoinedObject.AddComponent<TextMeshProUGUI>();
                lastJoinedText.alignment = TextAlignmentOptions.MidlineLeft;
                lastJoinedText.material = UIFactory.DefaultTextMesh.Material;
                lastJoinedText.color = UIFactory.DefaultTextMesh.Color;
                lastJoinedText.fontStyle = FontStyles.Italic;
                var lastJoined = GetLastJoined(record.JoinedAt);
                lastJoinedText.SetText(lastJoined);

                var joinButtonObject = UIFactory.CreateButton(recordObject.transform);
                var joinButtonRect = joinButtonObject.GetComponent<RectTransform>();
                joinButtonRect.pivot = new Vector2(1, 0.5f);
                joinButtonObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                joinButtonObject.AddComponent<LayoutElement>().preferredWidth = 160;
                var joinButtonText = joinButtonObject.GetComponentInChildren<TextMeshProUGUI>();
                var joinText = new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.JoinMenu.JoinButton.Key };
                joinButtonText.SetText(joinText);
                var joinButton = joinButtonObject.GetComponent<OwlcatButton>();
                joinButton.OnLeftClick.AddListener(() => ConnectToAddress(record.Address, joinButtonObject));
            }
        }

        private string GetLastJoined(DateTime joinedAt)
        {
            var elapsed = DateTime.UtcNow - joinedAt;
            if (elapsed.TotalSeconds <= 59)
            {
                return string.Format(new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.JoinMenu.DirectConnection.ServerHistory.SecondsAgo.Key }, Math.Max(1, Math.Round(elapsed.TotalSeconds, 0)));
            }
            else if (elapsed.TotalMinutes <= 59)
            {
                return string.Format(new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.JoinMenu.DirectConnection.ServerHistory.MinutesAgo.Key }, Math.Max(1, Math.Round(elapsed.TotalMinutes, 0)));
            }
            else if (elapsed.TotalHours <= 23)
            {
                return string.Format(new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.JoinMenu.DirectConnection.ServerHistory.HoursAgo.Key }, Math.Max(1, Math.Round(elapsed.TotalHours, 0)));
            }

            return string.Format(new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.JoinMenu.DirectConnection.ServerHistory.DaysAgo.Key }, Math.Max(1, Math.Round(elapsed.TotalDays, 0)));
        }

        private void OnDirectConnectJoinButtonClicked()
        {
            var address = ServerAddressObject.GetComponent<TMP_InputField>().text.Trim();
            ConnectToAddress(address, DirectConnectJoinButtonObject);
        }

        private void OnGameCodeConnectJoinButtonClicked()
        {
            var code = GameCodeInputObject.GetComponent<TMP_InputField>().text.Trim();
            if (string.IsNullOrEmpty(code))
            {
                _playerNotificationService.ShowModalMessage(WellKnownKeys.MultiplayerWindow.JoinMenu.GameCodeConnection.Errors.EmptyCode.Key);
                return;
            }

            var prefix = GetCodePrefixPart(code);
            if (string.IsNullOrEmpty(prefix))
            {
                _playerNotificationService.ShowModalMessage(WellKnownKeys.MultiplayerWindow.JoinMenu.GameCodeConnection.Errors.InvalidCodeFormat.Key);
                return;
            }

            var servers = GetExternalServers();
            var server = servers.FirstOrDefault(s => prefix == s.Prefix);
            if (server == null)
            {
                _playerNotificationService.ShowModalMessage(WellKnownKeys.MultiplayerWindow.JoinMenu.GameCodeConnection.Errors.UnroutedCode.Key, ServersFilePath);
                return;
            }

            var password = GamePasswordInputObject.GetComponent<TMP_InputField>().text;

            SetJoiningButtonsState(true, GameCodeConnectJoinButtonObject, [GameCodeConnectJoinButtonObject.GetComponent<OwlcatButton>()]);

            _logger.LogInformation("Connect via game code. Server={Server}, Code={Code}, HasPassword={HasPassword}", server.Name, code, !string.IsNullOrEmpty(password));
            _multiplayerClient.Connect(code, password, server);
        }

        private string GetCodePrefixPart(string code)
        {
            const string delimiter = "::";

            var prefixIndex = code.IndexOf(delimiter);
            var prefix = prefixIndex >= 0 ? code.Substring(0, prefixIndex) : null;
            return prefix;
        }

        private void ConnectToAddress(string address, GameObject initiator)
        {
            var endpoint = _ipEndPointParser.Parse(address);
            if (endpoint == null)
            {
                _playerNotificationService.ShowModalMessage(WellKnownKeys.MultiplayerClient.Errors.InvalidAddress.Key);
                return;
            }

            if (endpoint.Port == 0)
            {
                _playerNotificationService.ShowModalMessage(WellKnownKeys.MultiplayerClient.Errors.InvalidPort.Key);
                return;
            }

            _multiplayerClient.Connect(endpoint.Address.ToString(), endpoint.Port);
            SetJoiningState(true, initiator);
        }

        private void SetJoiningState(bool isJoining, GameObject initiator)
        {
            SetJoiningButtonsState(isJoining, initiator, [GameCodeConnectJoinButtonObject.GetComponent<OwlcatButton>(), DirectConnectJoinButtonObject.GetComponent<OwlcatButton>(), .. ServerHistoryRecords.GetComponentsInChildren<OwlcatButton>()]);
        }

        private void SetJoiningButtonsState(bool isJoining, GameObject initiator, params OwlcatButton[] buttons)
        {
            ConnectionMethodDropdown.interactable = !isJoining;

            foreach (var button in buttons)
            {
                button.Interactable = !isJoining;

                var buttonLabel = button.GetComponentInChildren<TextMeshProUGUI>();
                buttonLabel.DOKill();
                buttonLabel.SetText(new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.JoinMenu.JoinButton.Key });
                if (isJoining && button.gameObject == initiator)
                {
                    buttonLabel.SetText(string.Empty);
                    buttonLabel.DOText(new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.JoinMenu.ConnectingText.Key }, 2f)
                        .SetEase(Ease.Linear)
                        .SetLoops(-1, LoopType.Restart);
                }
            }
        }

        private void AddSuccessfulConnectionRecord(GameConnectivity connectivity)
        {
            var settings = _multiplayerSettingsService.GetSettings();
            if (!settings.TrackConnectionHistory)
            {
                return;
            }

            var record = new ConnectionHistoryRecord
            {
                Address = connectivity.Endpoint.ToString(),
                JoinedAt = DateTime.UtcNow
            };
            if (ConnectionHistory.TryGetValue(record, out var existingAddress))
            {
                existingAddress.JoinedAt = record.JoinedAt;
            }
            else
            {
                ConnectionHistory.Add(record);
            }

            var overflow = ConnectionHistory.Count - settings.MaxConnectionHistoryRecords;
            if (overflow > 0)
            {
                var itemsToRemove = ConnectionHistory.OrderByDescending(x => x.JoinedAt).Skip(settings.MaxConnectionHistoryRecords);
                foreach (var item in itemsToRemove)
                {
                    ConnectionHistory.Remove(item);
                }
            }

            PersistConnectionHistory();
        }

        private void PersistConnectionHistory()
        {
            var fullPath = Path.GetFullPath(ConnectionHistoryFilePath);
            var json = JsonConvert.SerializeObject(ConnectionHistory);
            var isSaved = FileSystemService.WriteFile(fullPath, json);
            if (!isSaved)
            {
                _playerNotificationService.ShowModalMessage(WellKnownKeys.MultiplayerWindow.JoinMenu.DirectConnection.ServerHistory.Errors.UnableToSave.Key);
            }
        }

        private HashSet<ConnectionHistoryRecord> LoadConnectionHistory()
        {
            var settings = _multiplayerSettingsService.GetSettings();
            try
            {
                var content = FileSystemService.GetFileContent(ConnectionHistoryFilePath);
                if (content == null)
                {
                    return [];
                }

                var history = JsonConvert.DeserializeObject<HashSet<ConnectionHistoryRecord>>(content);
                return history;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to deserialize connection history");
                if (settings.TrackConnectionHistory)
                {
                    _playerNotificationService.ShowModalMessage(WellKnownKeys.MultiplayerWindow.JoinMenu.DirectConnection.ServerHistory.Errors.UnableToLoad.Key);
                }

                return [];
            }
        }

        private class ConnectionHistoryRecord
        {
            public string Address { get; set; }

            public DateTime JoinedAt { get; set; }

            public override int GetHashCode()
            {
                return (Address ?? string.Empty).GetHashCode();
            }

            public override bool Equals(object obj)
            {
                return obj is ConnectionHistoryRecord another && another.Address == this.Address;
            }
        }

        private class ConnectionMethodOptionData : TMP_Dropdown.OptionData
        {
            public ConnectionMethodType Method { get; set; }

            public ConnectionMethodOptionData(ConnectionMethodType connectionMethodType, string text)
            {
                Method = connectionMethodType;
                base.text = text;
            }
        }

        private enum ConnectionMethodType
        {
            DirectIP,
            GameCode
        }
    }
}
