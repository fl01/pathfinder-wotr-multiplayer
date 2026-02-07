using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DG.Tweening;
using Kingmaker.Localization;
using Kingmaker.PubSubSystem;
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
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.Settings;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Controllers.Menu;
using WOTRMultiplayer.Abstractions.Unity;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.UI.Windows;
namespace WOTRMultiplayer.UI.Controllers
{
    public class JoinMenuItemController : MenuItemControllerBase, IJoinMenuItemController
    {
        public const string ConnectionHistoryFilePath = $"{Main.ModFolder}/data/connections.json";

        public const string RootContentScreenObjectName = "RootContentScreen";
        public const string JoinMenuItemContentObjectName = "JoinMenuItemContent";
        public const string JoinLobbyControlsMenuObjectName = "JoinLobbyControlsMenu";
        public const string ServerAddressInputObjectName = "ServerAddressInput";
        public const string JoinServerButtonObjectName = "JoinServerButton";

        public const string LobbyControlsMenuObjectName = "LobbyControlsMenu";
        public const string LobbyControlsMenuReadyButtonObjectName = "ReadyButton";
        public const string LobbyControlsMenuLeaveButtonObjectName = "LeaveButton";

        public const string ServerHistoryObjectName = "ServerHistory";
        public const string ServerHistoryHeaderObjectName = "ServerHistoryHeader";
        public const string ServerHistoryRecordsObjectName = "ServerHistoryRecords";
        public const string ServerHistoryRecordsBorderObjectName = "ServerHistoryRecordsBorder";

        public const string LobbyWindowObjectName = "LobbyWindow";

        public const string GameTitleObjectName = "LobbyTitleObject";

        private readonly ILogger<JoinMenuItemController> _logger;
        private readonly IUIFactory _uiFactory;
        private readonly IMultiplayerClient _multiplayerClient;
        private readonly IFileSystemService _fileSystemService;
        private readonly IMultiplayerSettingsService _multiplayerSettingsService;

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

        protected GameObject ServerHistoryRecords => JoinLobbyControlsObject.transform
            .Find(ServerHistoryObjectName)
            .Find(ServerHistoryRecordsObjectName)
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
            IMultiplayerSettingsService multiplayerSettingsService,
            IFileSystemService fileSystemService,
            IUIFactory uiFactory)
            : base(logger, lobbyWindowController, mainThreadAccessor, resourceProvider, multiplayerClient)
        {
            _logger = logger;
            _uiFactory = uiFactory;
            _multiplayerClient = multiplayerClient;
            _fileSystemService = fileSystemService;
            _multiplayerSettingsService = multiplayerSettingsService;
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

            var content = _uiFactory.CreateDefaultGameObject(_menuContent.transform);
            content.name = RootContentScreenObjectName;
            content.AddComponent<VerticalLayoutGroup>();
            var gameTitle = _uiFactory.CreateDefaultGameObject(content.transform);
            gameTitle.name = GameTitleObjectName;
            var title = gameTitle.AddComponent<TextMeshProUGUI>();
            title.alignment = TextAlignmentOptions.Center;
            title.fontSize = 28;
            title.material = _uiFactory.DefaultTextMesh.Material;
            title.color = _uiFactory.DefaultTextMesh.Color;
            var gameTitleVertical = gameTitle.AddComponent<VerticalLayoutGroup>();
            gameTitleVertical.padding = new RectOffset(0, 0, 0, 55);

            var lobbyWindow = _uiFactory.CreateDefaultGameObject(content.transform);
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
            var lobbyControlsMenu = _uiFactory.CreateDefaultGameObject(content.transform);
            lobbyControlsMenu.name = LobbyControlsMenuObjectName;
            lobbyControlsMenu.AddComponent<HorizontalLayoutGroup>();
            lobbyControlsMenu.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
            var lobbyControlsMenuLayout = lobbyControlsMenu.AddComponent<LayoutElement>();
            lobbyControlsMenuLayout.preferredHeight = menuContentRect.sizeDelta.y * 0.07f;

            var readyButtonObject = _uiFactory.CreateButton(lobbyControlsMenu.transform);
            readyButtonObject.name = LobbyControlsMenuReadyButtonObjectName;
            var readyButtonObjectLayout = readyButtonObject.AddComponent<LayoutElement>();
            readyButtonObjectLayout.preferredWidth = menuContentRect.sizeDelta.x * 0.2f;
            readyButtonObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var readyButton = readyButtonObject.GetComponent<OwlcatButton>();
            readyButton.OnLeftClick.AddListener(OnReadyButtonClicked);

            var leaveButtonObject = _uiFactory.CreateButton(lobbyControlsMenu.transform);
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
            // input + button ?
            var joinLobbyControlsMenu = _uiFactory.CreateDefaultGameObject(parent);
            joinLobbyControlsMenu.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            joinLobbyControlsMenu.name = JoinLobbyControlsMenuObjectName;
            joinLobbyControlsMenu.AddComponent<VerticalLayoutGroup>();
            joinLobbyControlsMenu.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var serverInfoInputObject = _uiFactory.CreateInput(joinLobbyControlsMenu.transform);
            serverInfoInputObject.name = ServerAddressInputObjectName;
            var serverInfoInputObjectLayout = serverInfoInputObject.AddComponent<LayoutElement>();
            serverInfoInputObjectLayout.preferredHeight = 35;
            var serverPlaceholder = serverInfoInputObject.transform.Find(UIFactory.InputPlaceholderObjectName);
            var serverPlaceholderInput = serverPlaceholder.GetComponent<TextMeshProUGUI>();
            serverPlaceholderInput.SetText(new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.JoinMenu.ServerAddress.Placeholder.Key });
            serverPlaceholderInput.alignment = TextAlignmentOptions.Center;
            var serverInfoInputLabelObject = serverInfoInputObject.transform.Find(UIFactory.InputLabelObjectName);
            var serverInfoInput = serverInfoInputLabelObject.GetComponent<TextMeshProUGUI>();
            serverInfoInput.overflowMode = TextOverflowModes.Truncate;
            serverInfoInput.alignment = TextAlignmentOptions.Center;

            var joinLobbyButtonObject = _uiFactory.CreateButton(joinLobbyControlsMenu.transform);
            joinLobbyButtonObject.name = JoinServerButtonObjectName;
            var joinLobbyButtonObjectLayout = joinLobbyButtonObject.AddComponent<LayoutElement>();
            joinLobbyButtonObjectLayout.preferredWidth = fullSize.x * 0.35f;
            joinLobbyButtonObjectLayout.preferredHeight = 50;
            joinLobbyButtonObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var button = joinLobbyButtonObject.GetComponent<OwlcatButton>();
            button.OnLeftClick.AddListener(OnJoinButtonClicked);

            var serverHistoryContainer = _uiFactory.CreateDefaultGameObject(joinLobbyControlsMenu.transform);
            serverHistoryContainer.AddComponent<VerticalLayoutGroup>();
            serverHistoryContainer.name = ServerHistoryObjectName;
            var serverHistoryHeaderObject = _uiFactory.CreateDefaultGameObject(serverHistoryContainer.transform);
            serverHistoryHeaderObject.name = ServerHistoryHeaderObjectName;
            serverHistoryHeaderObject.AddComponent<VerticalLayoutGroup>().padding = new RectOffset(0, 0, 15, 35); ;
            var serverHistoryHeader = serverHistoryHeaderObject.AddComponent<TextMeshProUGUI>();
            var headerText = UIUtility.GetSaberBookFormat(new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.JoinMenu.ServerHistory.Header.Key });
            serverHistoryHeader.SetText(headerText);
            serverHistoryHeader.fontSize = 28;
            serverHistoryHeader.horizontalAlignment = HorizontalAlignmentOptions.Center;
            serverHistoryHeader.material = _uiFactory.DefaultTextMesh.Material;
            serverHistoryHeader.color = _uiFactory.DefaultTextMesh.Color;
            var serverHistoryRecordsObject = _uiFactory.CreateDefaultGameObject(serverHistoryContainer.transform);
            serverHistoryRecordsObject.name = ServerHistoryRecordsObjectName;
            serverHistoryRecordsObject.AddComponent<VerticalLayoutGroup>();
            var serverHistoryRecordsBorder = _uiFactory.CreateBorderDecoration(serverHistoryRecordsObject.transform);
            serverHistoryRecordsBorder.name = ServerHistoryRecordsBorderObjectName;
            var serverHistoryRecordsLayoutGroup = serverHistoryRecordsObject.AddComponent<VerticalLayoutGroup>();
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
        }

        protected override void DisposeInternal()
        {
            SetupHandlers(false);

            base.DisposeInternal();
        }

        private void OnMultiplayerConnected(NetworkGameConnectivity connectivity)
        {
            AddSuccessfulConnectionRecord(connectivity);

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
                var recordObject = _uiFactory.CreateDefaultGameObject(ServerHistoryRecords.transform);
                recordObject.AddComponent<HorizontalLayoutGroup>();
                recordObject.AddComponent<ContentSizeFitter>();
                recordObject.AddComponent<LayoutElement>().preferredHeight = 50;

                var serverAddressObject = _uiFactory.CreateDefaultGameObject(recordObject.transform);
                var serverAddressRect = serverAddressObject.GetComponent<RectTransform>();
                serverAddressRect.pivot = Vector2.zero;
                serverAddressObject.AddComponent<ContentSizeFitter>();
                serverAddressObject.AddComponent<LayoutElement>().preferredWidth = 250;
                var serverAddressText = serverAddressObject.AddComponent<TextMeshProUGUI>();
                serverAddressText.material = _uiFactory.DefaultTextMesh.Material;
                serverAddressText.color = _uiFactory.DefaultTextMesh.Color;
                serverAddressText.alignment = TextAlignmentOptions.MidlineLeft;
                serverAddressText.horizontalAlignment = HorizontalAlignmentOptions.Left;
                var serverAddress = settings.HideServerAddress ? "***.***.***.***:****" : record.Address;
                serverAddressText.SetText(serverAddress);

                var lastJoinedObject = _uiFactory.CreateDefaultGameObject(recordObject.transform);
                var lastJoinednRect = lastJoinedObject.GetComponent<RectTransform>();
                lastJoinednRect.pivot = Vector2.zero;
                var lastJoinedText = lastJoinedObject.AddComponent<TextMeshProUGUI>();
                lastJoinedText.alignment = TextAlignmentOptions.MidlineLeft;
                lastJoinedText.material = _uiFactory.DefaultTextMesh.Material;
                lastJoinedText.color = _uiFactory.DefaultTextMesh.Color;
                lastJoinedText.fontStyle = FontStyles.Italic;
                var lastJoined = GetLastJoined(record.JoinedAt);
                lastJoinedText.SetText(lastJoined);

                var joinButtonObject = _uiFactory.CreateButton(recordObject.transform);
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
                return string.Format(new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.JoinMenu.ServerHistory.SecondsAgo.Key }, Math.Max(1, Math.Round(elapsed.TotalSeconds, 0)));
            }
            else if (elapsed.TotalMinutes <= 59)
            {
                return string.Format(new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.JoinMenu.ServerHistory.MinutesAgo.Key }, Math.Max(1, Math.Round(elapsed.TotalMinutes, 0)));
            }
            else if (elapsed.TotalHours <= 23)
            {
                return string.Format(new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.JoinMenu.ServerHistory.HoursAgo.Key }, Math.Max(1, Math.Round(elapsed.TotalHours, 0)));
            }

            return string.Format(new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.JoinMenu.ServerHistory.DaysAgo.Key }, Math.Max(1, Math.Round(elapsed.TotalDays, 0)));
        }

        private void OnJoinButtonClicked()
        {
            var address = ServerAddressObject.GetComponent<TMP_InputField>().text.Trim();
            ConnectToAddress(address, JoinButtonObject);
        }

        private void ConnectToAddress(string address, GameObject initiator)
        {
            var result = _multiplayerClient.Connect(address);
            if (!result.IsOk)
            {
                var message = new LocalizedString { Key = result.MessageKey };
                EventBus.RaiseEvent<IMessageModalUIHandler>(x => x.HandleOpen(message, MessageModalBase.ModalType.Message));
                return;
            }

            SetJoiningState(true, initiator);
        }

        private void SetJoiningState(bool isJoining, GameObject initiator)
        {
            SetJoiningButtonsState(isJoining, initiator, [JoinButtonObject.GetComponent<OwlcatButton>(), .. ServerHistoryRecords.GetComponentsInChildren<OwlcatButton>()]);
        }

        private void SetJoiningButtonsState(bool isJoining, GameObject initiator, params OwlcatButton[] buttons)
        {
            foreach (var button in buttons)
            {
                button.Interactable = !isJoining;

                var buttonLabel = button.GetComponentInChildren<TextMeshProUGUI>();
                buttonLabel.DOKill();
                buttonLabel.SetText(new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.JoinMenu.JoinButton.Key });
                if (isJoining && button.gameObject == initiator)
                {
                    buttonLabel.SetText(string.Empty);
                    buttonLabel.DOText(new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.JoinMenu.ConnectingText.Key }, 2f).SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart);
                }
            }
        }


        private void AddSuccessfulConnectionRecord(NetworkGameConnectivity connectivity)
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
            var isSaved = _fileSystemService.WriteFile(fullPath, json);
            if (!isSaved)
            {
                var message = string.Format(new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.JoinMenu.ServerHistory.Errors.UnableToSave.Key }, fullPath);
                EventBus.RaiseEvent<IMessageModalUIHandler>(x => x.HandleOpen(message));
            }
        }

        private HashSet<ConnectionHistoryRecord> LoadConnectionHistory()
        {
            var settings = _multiplayerSettingsService.GetSettings();
            try
            {
                var content = _fileSystemService.GetFileContent(ConnectionHistoryFilePath);
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
                    var message = string.Format(new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.JoinMenu.ServerHistory.Errors.UnableToSave.Key }, ex.Message);
                    EventBus.RaiseEvent<IMessageModalUIHandler>(x => x.HandleOpen(message));
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
    }
}
