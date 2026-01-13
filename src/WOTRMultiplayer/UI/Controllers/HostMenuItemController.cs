using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.Localization;
using Kingmaker.UI.Common;
using Kingmaker.UI.MVVM;
using Kingmaker.UI.MVVM._PCView.SaveLoad;
using Kingmaker.UI.MVVM._VM.SaveLoad;
using Kingmaker.UI.MVVM._VM.ServiceWindows.CharacterInfo.Sections.Abilities;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.Controls.Button;
using Owlcat.Runtime.UI.SelectionGroup;
using Owlcat.Runtime.UI.VirtualListSystem;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.Events;
using WOTRMultiplayer.Abstractions;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Controllers.Menu;
using WOTRMultiplayer.Abstractions.Unity;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.UI.Windows;

namespace WOTRMultiplayer.UI.Controllers
{
    public class HostMenuItemController : MenuItemControllerBase, IHostMenuItemController
    {
        public const string NewGameSequenceId = "###WOTR_MP_NEW_GAME###";

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

        protected override GameObject ReadyButtonObject => Buttons.Find(ReadyButtonObjectName)?.gameObject;

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

        private OwlcatButton ReadyButton => ReadyButtonObject.GetComponent<OwlcatButton>();

        private GameObject StartButtonObject => Buttons.Find(StartButtonObjectName)?.gameObject;
        private OwlcatButton StartButton => StartButtonObject.GetComponent<OwlcatButton>();

        public HostMenuItemController(
            ILogger<HostMenuItemController> logger,
            IMultiplayerHost multiplayerHost,
            IMainThreadAccessor mainThreadAccessor,
            ILobbyWindowController lobbyWindowController,
            IResourceProvider resourceProvider)
            : base(logger, lobbyWindowController, mainThreadAccessor, resourceProvider, multiplayerHost)
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

            AddNewGameSaveSlot(_saveLoadViewModel);

            if (SetupLayout)
            {
                SetupLayout = false;
                /// overriding save/load/delete buttons prefab to make sure original loadsave screen is not affected
                var screen = saveLoadView.gameObject.transform.Find(SaveLoadScreen);
                var collectionView = screen.Find("SaveSlotCollectionPlace").Find("SaveSlotVirtualCollectionView");
                var virtualView = collectionView.GetComponent<SaveSlotCollectionVirtualView>();
                var prefab = virtualView.m_SaveSlotPrefab as SaveSlotPCView;
                var copyPrefabObj = UnityEngine.Object.Instantiate(prefab.gameObject, prefab.transform.parent);
                var newPrefabView = copyPrefabObj.GetComponent<SaveSlotPCView>();
                virtualView.m_VirtualList.Initialize(new VirtualListElementTemplate<ExpandableTitleVM>(virtualView.m_ExpandableTitleView), new VirtualListElementTemplate<SaveSlotVM>(newPrefabView));
                UnityEngine.Object.DestroyImmediate(newPrefabView.m_SaveLoadButton.gameObject);
                UnityEngine.Object.DestroyImmediate(newPrefabView.m_DeleteButton.gameObject);
            }

            SetupButtons();

            SetupHandlers(true);

            saveLoadView.Bind(_saveLoadViewModel);
            saveLoadView.AddDisposable(_saveLoadViewModel.SelectedSaveSlot.Subscribe(slot =>
            {
                HostButton.Interactable = slot != null;
            }));
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

        protected override void InitializeInternal(GameObject baseLayout)
        {
            var label = MenuItem.GetComponentInChildren<TextMeshProUGUI>();
            label.SetText(new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.HostMenu.Title.Key });

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
            _multiplayerHost.OnNewGameSequenceStarted = enable ? OnMultiplayerNewGameSequenceStarted : null;
            _multiplayerHost.OnCharactersChanged = enable ? OnMultiplayerCharactersChanged : null;

            Lobby.OnCharacterOwnerChanged = enable ? OnLobbyCharacterOwnerChanged : null;
        }

        protected override void DisposeInternal()
        {
            SetupHandlers(false);

            base.DisposeInternal();
        }

        private void AddNewGameSaveSlot(SaveLoadVM saveLoadVM)
        {
            // fake save so it can be selected to start a new game
            var saveInfo = new Kingmaker.EntitySystem.Persistence.SaveInfo
            {
                Name = new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.HostMenu.NewGame.SaveSlotTitle.Key },
                PlayerCharacterName = new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.HostMenu.NewGame.SaveSlotGroupName.Key },
                GameTotalTime = TimeSpan.FromMilliseconds(1),
                GameId = NewGameSequenceId,
                PartyPortraits = [],
                Screenshot = ResourceProvider.GetTexture2D(WellKnownSpriteBundles.UI, "UI_PFWOTR_NewGameMainStory")
            };
            var saveSlot = new SaveSlotVM(saveInfo, saveLoadVM.Mode, null, null);
            saveSlot.SaveTime.Value = string.Empty;
            saveLoadVM.AddDisposable(saveSlot);
            var saveSlotGroup = new SaveSlotGroupVM(saveSlot);
            saveSlotGroup.IsExpanded.Value = true;
            saveSlotGroup.IsFirst = true;
            saveLoadVM.SaveSlotCollectionVm.AddDisposable(saveSlotGroup);
            saveSlotGroup.HandleNewSave(saveSlot);
            saveLoadVM.SaveSlotCollectionVm.AllTitlesAndSlots.Insert(0, saveSlotGroup.ExpandableTitleVM);
            saveLoadVM.SaveSlotCollectionVm.AllTitlesAndSlots.Insert(1, saveSlot);

            saveLoadVM.m_SaveSlotVMs.Add(saveSlot);

            saveLoadVM.m_SelectionGroup.Dispose();
            saveLoadVM.AddDisposable(_saveLoadViewModel.m_SelectionGroup = new SelectionGroupRadioVM<SaveSlotVM>(saveLoadVM.m_SaveSlotVMs, saveLoadVM.SelectedSaveSlot, false));
        }

        private void SetupButtons()
        {
            SetButtonLabel(HostButtonObject, new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.HostMenu.HostButton.HostText.Key });
            SetupButtonClick(HostButton, OnHostButtonClicked);
            HostButton.Interactable = false;

            SetButtonLabel(ReadyButtonObject, new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.ReadyButton.ReadyText.Key });
            SetupButtonClick(ReadyButton, OnReadyButtonClicked);
            ReadyButtonObject.SetActive(false);
            ReadyButton.Interactable = false;

            SetButtonLabel(StartButtonObject, new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.HostMenu.StartButton.Key });
            SetupButtonClick(StartButton, OnStartButtonClicked);
            StartButtonObject.SetActive(false);
            StartButton.Interactable = false;
        }

        private void OnHostButtonClicked()
        {
            var selectedSave = _saveLoadViewModel.SelectedSaveSlot.Value;
            var gameName = selectedSave.SaveName.Value;
            var titleText = UIUtility.GetSaberBookFormat(gameName);
            Title.SetText(titleText);

            var (gameId, startup) = CreateGameStartUp(selectedSave);
            if (!_multiplayerHost.IsActive)
            {
                StartButtonObject.SetActive(true);
                ReadyButtonObject.SetActive(true);
                ReadyButton.Interactable = true;
                _multiplayerHost.Create(gameId, startup);
                SetButtonLabel(HostButtonObject, new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.HostMenu.HostButton.SelectSaveText.Key });
                _logger.LogInformation("Hosted new game");
                return;
            }

            _multiplayerHost.ChangeHostedStartingPoint(gameId, startup);
            _logger.LogInformation("Updated hosted game");
        }

        private (string, NetworkGameStartUp) CreateGameStartUp(SaveSlotVM saveSlot)
        {
            if (string.Equals(saveSlot.GameId.Value, NewGameSequenceId, StringComparison.OrdinalIgnoreCase))
            {
                var mainCharacterId = Guid.NewGuid().ToString();
                var newGameSequence = new NetworkGameStartUp(null)
                {
                    // empty character that can be used to assign control for leveling (chargen) screen
                    Characters = [new NetworkCharacter { Portrait = "b7aa1433ab20e3745a4a169ee34ca738_MaskGolem", UnitId = mainCharacterId }],
                };

                var gameId = Guid.NewGuid().ToString("N");
                _logger.LogInformation("Fake new campaign startup has been generated. GameId={GameId}, MainCharacterId={MainCharacterId}", gameId, mainCharacterId);
                return (gameId, newGameSequence);
            }

            var portraits = saveSlot.PartyPortraits.Value.Select(p => p.Portrait.name).ToList();
            var characters = portraits.Select(x => new NetworkCharacter { Name = x, Portrait = x, Owner = null }).ToList();
            var savePath = saveSlot.Reference.FolderName;
            var saveGame = new NetworkGameStartUp(savePath)
            {
                Characters = characters
            };

            var mainCharacter = characters.FirstOrDefault();
            if (mainCharacter != null)
            {
                mainCharacter.Name = saveSlot.CharacterName.Value;
            }

            return (saveSlot.GameId.Value, saveGame);
        }

        private void OnStartButtonClicked()
        {
            _logger.LogInformation("OnStartButton");
            var hasStarted = _multiplayerHost.Start();
            StartButton.Interactable = !hasStarted;
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

        private void OnLobbyCharacterOwnerChanged(NetworkCharacter character, NetworkPlayer player)
        {
            _logger.LogInformation("OnLobbyCharacterOwnerChanged. CharacterName={CharacterName}, PlayerId={PlayerId}", character.Name, player.Id);
            _multiplayerHost.ChangeCharacterOwner(character, player);
        }

        private void OnMultiplayerConnected(NetworkGameConnectivity connectivity)
        {
            Lobby.UpdateServerInfo(connectivity);
        }

        private void OnMultiplayerPlayersChanged(NetworkLobbyStage lobbyStage, List<NetworkPlayer> players)
        {
            Lobby.UpdatePlayers(players);
            var canStart = lobbyStage == NetworkLobbyStage.Lobby && players.All(p => p.IsReady);
            MainThreadAccessor.Post(() =>
            {
                StartButton.Interactable = canStart;
            });
        }

        private void OnMultiplayerCharactersChanged(List<NetworkCharacter> characters)
        {
            Lobby.UpdateCharacters(characters, isDropdownInteractable: true);
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
                    MessageKey = WellKnownKeys.MultiplayerWindow.HostMenu.Deactivation.Hosting.Key
                };
            }

            return null;
        }
    }
}
