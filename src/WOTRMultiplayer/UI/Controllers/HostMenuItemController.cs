using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.UI.Common;
using Kingmaker.UI.MVVM;
using Kingmaker.UI.MVVM._PCView.SaveLoad;
using Kingmaker.UI.MVVM._VM.SaveLoad;
using Kingmaker.UI.MVVM._VM.ServiceWindows.CharacterInfo.Sections.Abilities;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.Controls.Button;
using Owlcat.Runtime.UI.VirtualListSystem;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.Events;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.MP.Actors;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Controllers.Menu;
using WOTRMultiplayer.Abstractions.Unity;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.UI.Menu;

namespace WOTRMultiplayer.UI.Controllers
{
    public class HostMenuItemController : MenuItemControllerBase, IHostMenuItemController, IObserver<SaveSlotVM>
    {
        public const string HostMenuItemContentObjectName = "HostMenuItemContent";
        public const string SaveLoadView = "SaveLoadView";
        public const string SaveLoadScreen = "SaveLoadScreen";
        public const string SaveLoadDetails = "SaveLoadDetails";
        public const string SaveLoadDetailsTitle = "Title";
        public const string SaveLoadDetailsInfo = "Info";
        public const string SaveLoadDetailsInfoButtons = "Buttons";

        public const string HostButtonObjectName = "HostButton";
        public const string ReadyButtonObjectName = "ReadyButton";
        public const string StartButtonObjectName = "StartButton";

        private readonly ILogger<HostMenuItemController> _logger;
        private readonly IMultiplayerHost _multiplayerHost;

        private GameObject _menuContent;

        private SaveLoadVM _saveLoadViewModel;
        private SaveLoadPCView _saveLoadView;

        protected override GameObject MenuContent => _menuContent;
        protected override LobbyWindowOwner Owner => LobbyWindowOwner.HostMenu;

        private TextMeshProUGUI Title => _menuContent
            .transform
            .Find(SaveLoadView)
            .Find(SaveLoadScreen)
            .Find(SaveLoadDetails)
            .Find(SaveLoadDetailsTitle)
            .gameObject
            .GetComponentInChildren<TextMeshProUGUI>();

        private Transform Buttons => _menuContent
            .transform
            .Find(SaveLoadView)
            .Find(SaveLoadScreen)
            .Find(SaveLoadDetails)
            .Find(SaveLoadDetailsInfo)
            .Find(SaveLoadDetailsInfoButtons);

        private GameObject HostButtonObject => Buttons.Find(HostButtonObjectName)?.gameObject;
        private OwlcatButton HostButton => HostButtonObject.GetComponent<OwlcatButton>();

        private GameObject ReadyButtonObject => Buttons.Find(ReadyButtonObjectName)?.gameObject;
        private OwlcatButton ReadyButton => ReadyButtonObject.GetComponent<OwlcatButton>();

        private GameObject StartButtonObject => Buttons.Find(StartButtonObjectName)?.gameObject;
        private OwlcatButton StartButton => StartButtonObject.GetComponent<OwlcatButton>();

        public HostMenuItemController(
            ILogger<HostMenuItemController> logger,
            IMultiplayerHost multiplayerHost,
            IMainThreadAccessor mainThreadAccessor,
            ILobbyWindowController lobbyWindowController,
            IGameInteractionService gameInteractionService)
            : base(logger, lobbyWindowController, mainThreadAccessor, gameInteractionService)
        {
            _logger = logger;
            _multiplayerHost = multiplayerHost;
        }

        public override void Activate()
        {
            _logger.LogInformation("Trying to activate");

            if (IsActive)
            {
                return;
            }

            Lobby.SetActiveOwner(LobbyWindowOwner.HostMenu);

            var saveLoadView = _menuContent.transform.GetChild(0).GetComponent<SaveLoadPCView>();
            _saveLoadViewModel = new SaveLoadVM(SaveLoadMode.Load, true, DisposeSaveLoadVM, RootUIContext.Instance.CommonVM);

            if (SetupLayout)
            {
                SetupLayout = false;
                /// overriding save/load/delete buttons prefab to make sure original loadsave screen is not affected
                var screen = saveLoadView.gameObject.transform.Find(SaveLoadScreen);
                var collectionView = screen.Find("SaveSlotCollectionPlace").Find("SaveSlotVirtualCollectionView");
                var virtualView = collectionView.GetComponent<SaveSlotCollectionVirtualView>();
                var prefab = virtualView.m_SaveSlotPrefab as SaveSlotPCView;
                var copyPrefabObj = UnityEngine.Object.Instantiate(prefab.gameObject, prefab.transform.parent);
                var newPrefab = copyPrefabObj.GetComponent<SaveSlotPCView>();
                virtualView.m_VirtualList.Initialize(new VirtualListElementTemplate<ExpandableTitleVM>(virtualView.m_ExpandableTitleView), new VirtualListElementTemplate<SaveSlotVM>(newPrefab));
                UnityEngine.Object.DestroyImmediate(newPrefab.m_SaveLoadButton.gameObject);
                UnityEngine.Object.DestroyImmediate(newPrefab.m_DeleteButton.gameObject);
                ///

            }

            SetupButtons();

            SetupHandlers(true);

            saveLoadView.Bind(_saveLoadViewModel);
            _saveLoadViewModel.SelectedSaveSlot.Subscribe(this);
            Game.Instance.UI.EscManager.Unsubscribe(_saveLoadViewModel.OnClose);

            saveLoadView.Show();
            base.Activate();
        }

        public override void Deactivate()
        {
            Title.SetText(string.Empty);

            if (!IsActive)
            {
                return;
            }

            if (_multiplayerHost.IsInLobby)
            {
                _multiplayerHost.Reset();
            }

            SetupHandlers(false);

            Lobby.ResetData();

            DisposeSaveLoadVM();
            base.Deactivate();
        }

        public void OnNext(SaveSlotVM value)
        {
            HostButton.Interactable = value != null;
        }

        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }

        protected override void InitializeInternal(GameObject baseLayout)
        {
            var label = MenuItem.GetComponentInChildren<TextMeshProUGUI>();
            label.SetText(UIStringConsts.MultiplayerWindow.HostMenuLabel);

            _menuContent = UnityEngine.Object.Instantiate(baseLayout, baseLayout.transform);
            _menuContent.name = HostMenuItemContentObjectName;
            _menuContent.CleanupAllChildren();
            _menuContent.SetActive(false);

            SetupLoadSaveGamesLayout();
            SetupLobbyInfo(baseLayout);

            Title.SetText(string.Empty);
        }

        private void SetupHandlers(bool enable)
        {
            _multiplayerHost.OnConnected = enable ? OnMultiplayerConnected : null;
            _multiplayerHost.OnPlayersChanged = enable ? OnMultiplayerPlayersChanged : null;
            _multiplayerHost.OnStartGame = enable ? OnMultiplayerStartGame : null;

            Lobby.OnCharacterOwnerChanged = enable ? OnLobbyCharacterOwnerChanged : null;
        }

        private void OnMultiplayerStartGame(string saveFilePath)
        {
            _logger.LogInformation("Starting multiplayer game as a host");
            GameInteraction.LoadGameFromMainMenu(saveFilePath);
        }

        protected override void DisposeInternal()
        {
            SetupHandlers(false);

            base.DisposeInternal();
        }

        private void SetupButtons()
        {
            SetButtonLabel(HostButtonObject, UIStringConsts.MultiplayerWindow.HostMenu.HostButtonLabel);
            SetupButtonClick(HostButton, OnHostButtonClicked);
            HostButton.Interactable = false;

            SetButtonLabel(ReadyButtonObject, UIStringConsts.MultiplayerWindow.HostMenu.ReadyNotReadyButtonLabel);
            SetupButtonClick(ReadyButton, OnReadyButtonClicked);
            ReadyButtonObject.SetActive(false);
            ReadyButton.Interactable = false;

            SetButtonLabel(StartButtonObject, UIStringConsts.MultiplayerWindow.HostMenu.StartButtonLabel);
            SetupButtonClick(StartButton, OnStartButtonClicked);
            StartButtonObject.SetActive(false);
            StartButton.Interactable = false;
        }

        private void OnHostButtonClicked()
        {
            _logger.LogInformation("OnHostButton");
            var selectedSave = _saveLoadViewModel.SelectedSaveSlot.Value;
            var gameName = selectedSave.SaveName.Value;
            var titleText = UIUtility.GetSaberBookFormat(gameName);
            Title.SetText(titleText);

            var portraits = selectedSave.PartyPortraits.Value.Select(p => p.Portrait.name).ToList();
            var savePath = selectedSave.Reference.FolderName;
            var characters = portraits.Select(x => new NetworkCharacterOwnership { Name = x, Portrait = x, Owner = null }).ToList();
            characters.First().Name = selectedSave.CharacterName.Value;
            Lobby.UpdateCharacters(characters, true);

            if (!_multiplayerHost.IsActive)
            {
                StartButtonObject.SetActive(true);
                ReadyButtonObject.SetActive(true);
                ReadyButton.Interactable = true;
                _multiplayerHost.Create(savePath, selectedSave.GameId.Value, characters);
                SetButtonLabel(HostButtonObject, UIStringConsts.MultiplayerWindow.HostMenu.HostButtonActiveLabel);
                return;
            }

            _multiplayerHost.UpdateSaveGame(savePath, selectedSave.GameId.Value, characters);
        }

        private void OnReadyButtonClicked()
        {
            _logger.LogInformation("OnReadyButton");
            var isReady = _multiplayerHost.ReadyChanged();
            var label = isReady ? UIStringConsts.MultiplayerWindow.HostMenu.ReadyButtonLabel
                : UIStringConsts.MultiplayerWindow.HostMenu.ReadyNotReadyButtonLabel;
            SetButtonLabel(ReadyButtonObject, label);
        }

        private void OnStartButtonClicked()
        {
            _logger.LogInformation("OnStartButton");
            StartButton.Interactable = false;
            _multiplayerHost.Start();
        }

        private void SetButtonLabel(GameObject buttonObject, string text)
        {
            buttonObject.GetComponentInChildren<TextMeshProUGUI>().SetText(text);
        }

        private void SetupButtonClick(OwlcatButton button, Action handler)
        {
            button.OnLeftClick.RemoveAllListeners();
            button.OnLeftClick.AddListener(new UnityAction(handler));
        }

        private void SetupLobbyInfo(GameObject baseLayout)
        {
            var saveLoadView = _menuContent.transform.GetChild(0);
            var screen = saveLoadView.gameObject.transform.Find("SaveLoadScreen");
            var container = screen.Find(SaveLoadDetails);
            var replacedContainer = UnityEngine.Object.Instantiate(container, screen.transform);
            replacedContainer.name = SaveLoadDetails;
            // hack to replace container to get rid of existing unity references
            // fakebutton must be kept intact
            container.name = "OLD_" + SaveLoadDetails;
            container.gameObject.CleanupAllChildren(x => x.name != "FakeButton");
            container.gameObject.SetActive(false);

            replacedContainer.gameObject.SetActive(true);
            var parentContainerRect = replacedContainer.GetComponent<RectTransform>();

            var lobbyWindowObject = UnityEngine.Object.Instantiate(baseLayout, replacedContainer.transform);
            lobbyWindowObject.name = "MultiplayerLobby";
            lobbyWindowObject.CleanupAllChildren();
            var title = replacedContainer.Find(SaveLoadDetailsTitle);
            var lobbyWindowObjectPosition = new Vector3(title.transform.position.x, lobbyWindowObject.transform.position.y * 1.1f, lobbyWindowObject.transform.position.z);
            lobbyWindowObject.transform.SetPositionAndRotation(lobbyWindowObjectPosition, lobbyWindowObject.transform.rotation);
            var lobbyWindowObjectRect = lobbyWindowObject.GetComponent<RectTransform>();
            lobbyWindowObjectRect.sizeDelta = new Vector2(parentContainerRect.sizeDelta.x * 0.9f, parentContainerRect.sizeDelta.y * 0.72f);

            Lobby.InitializeContent(LobbyWindowOwner.HostMenu, lobbyWindowObject.transform);
        }

        private void OnLobbyCharacterOwnerChanged(int characterIndex, int playerIndex)
        {
            _logger.LogInformation("OnLobbyCharacterOwnerChanged. CharacterIndex={CharacterIndex}, PlayerIndex={PlayerIndex}", characterIndex, playerIndex);
            _multiplayerHost.ChangeCharacterOwner(characterIndex, playerIndex);
        }

        private void OnMultiplayerConnected(NetworkGameConnectivity connectivity)
        {
            Lobby.UpdateServerInfo(connectivity);
        }

        private void OnMultiplayerPlayersChanged(List<NetworkPlayer> players)
        {
            Lobby.UpdatePlayers(players);
            var canStart = players.All(p => p.IsReady);
            MainThreadAccessor.Post(() =>
            {
                StartButton.Interactable = canStart;
            });
            _logger.LogInformation("Players changed. CanStart={CanStart}", canStart);
        }

        private void SetupLoadSaveGamesLayout()
        {
            _saveLoadView = Main.Multiplayer.Factory.CreateSaveLoadPCView(_menuContent.transform);
            _saveLoadView.Initialize();
        }

        private void DisposeSaveLoadVM()
        {
            _logger.LogInformation("Disposing SaveLoadVM");
            _saveLoadViewModel?.Dispose();
            _saveLoadViewModel?.SelectedSaveSlot?.Dispose();
            _saveLoadViewModel = null;
        }

        protected override ModalActionConfirmation GetDeactivationConfirmationInternal()
        {
            if (_multiplayerHost.IsInLobby)
            {
                return new ModalActionConfirmation
                {
                    Text = UIStringConsts.MultiplayerWindow.HostMenu.TerminateServerMessage
                };
            }

            return null;
        }
    }
}
