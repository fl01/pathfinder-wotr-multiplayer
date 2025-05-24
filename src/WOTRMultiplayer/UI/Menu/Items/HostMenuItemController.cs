using System;
using Kingmaker.UI.MVVM;
using Kingmaker.UI.MVVM._PCView.SaveLoad;
using Kingmaker.UI.MVVM._VM.SaveLoad;
using Kingmaker.UI.MVVM._VM.ServiceWindows.CharacterInfo.Sections.Abilities;
using Owlcat.Runtime.UI.VirtualListSystem;
using TMPro;
using UnityEngine;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.Strings;
using WOTRMultiplayer.UI.Lobby;

namespace WOTRMultiplayer.UI.Menu.Items
{
    public class HostMenuItemController : MenuItemController, IObserver<SaveSlotVM>
    {
        public const string HostMenuItemContentObjectName = "HostMenuItemContent";

        private SaveLoadVM _saveLoadViewModel;
        private bool _setupLayout = true;
        private GameObject _menuContent;
        private LobbyInfoController _lobbyInfoController;

        public override GameObject MenuContent => _menuContent;

        public HostMenuItemController(MultiplayerWindow multiplayerWindow, GameObject menuItem)
            : base(multiplayerWindow, menuItem)
        {
        }

        public override void Activate()
        {
            Logging.Logger.Info($"Trying to activate {nameof(HostMenuItemController)}. IsActive={IsActive}");

            if (IsActive)
            {
                return;
            }

            var saveLoad = MenuContent.transform.GetChild(0).GetComponent<SaveLoadPCView>();
            _saveLoadViewModel = new SaveLoadVM(SaveLoadMode.Load, true, OnCloseSaveLoadVM, RootUIContext.Instance.CommonVM);

            if (_setupLayout)
            {
                _setupLayout = false;
                /// overriding save/load/delete buttons prefab to make sure original loadsave screen is not affected
                var screen = saveLoad.gameObject.transform.Find("SaveLoadScreen");
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

            saveLoad.Bind(_saveLoadViewModel);
            _saveLoadViewModel.SelectedSaveSlot.Subscribe(this);

            saveLoad.Show();
            base.Activate();
        }

        protected override void InitializeInternal(GameObject baseLayout)
        {
            var label = this.MenuItem.GetComponentInChildren<TextMeshProUGUI>();
            label.SetText(StringConsts.MultiplayerWindow.HostMenuLabel);

            _menuContent = UnityEngine.Object.Instantiate(baseLayout, baseLayout.transform);
            _menuContent.name = HostMenuItemContentObjectName;
            _menuContent.CleanupAllChildren();

            SetupLoadSaveGamesLayout();
            SetupLobbyInfo(baseLayout);
        }

        private void SetupLobbyInfo(GameObject baseLayout)
        {
            var saveLoadView = this.MenuContent.transform.GetChild(0);
            var screen = saveLoadView.gameObject.transform.Find("SaveLoadScreen");
            var container = screen.Find("SaveLoadDetails");

            var lobbyWindowObject = UnityEngine.Object.Instantiate(baseLayout, container.transform);
            lobbyWindowObject.name = "MultiplayerLobby";
            lobbyWindowObject.CleanupAllChildren();
            var title = container.Find("Title");
            var lobbyWindowObjectPosition = new Vector3(title.position.x, lobbyWindowObject.transform.position.y * 1.1f, lobbyWindowObject.transform.position.z);
            lobbyWindowObject.transform.SetPositionAndRotation(lobbyWindowObjectPosition, lobbyWindowObject.transform.rotation);
            var parentContainerRect = container.GetComponent<RectTransform>();
            var lobbyWindowObjectRect = lobbyWindowObject.GetComponent<RectTransform>();
            lobbyWindowObjectRect.sizeDelta = new Vector2(parentContainerRect.sizeDelta.x * 0.9f, parentContainerRect.sizeDelta.y * 0.72f);

            var lobbyContent = Main.Multiplayer.ElementFactory.CreateLobbyWindowContent(lobbyWindowObject.transform);
            _lobbyInfoController = new LobbyInfoController(lobbyContent);
        }

        private void SetupLoadSaveGamesLayout()
        {
            SaveLoadPCView saveLoad = Main.Multiplayer.ElementFactory.CreateSaveLoadPCView(this.MenuContent.transform);
            saveLoad.Initialize();
        }

        public override void Deactivate()
        {
            DisposeSaveLoadVM();
            base.Deactivate();
        }

        private void DisposeSaveLoadVM()
        {
            OnCompleted();
            _saveLoadViewModel?.Dispose();
        }

        private void OnCloseSaveLoadVM()
        {
            Window.OnCloseClicked();
        }

        public void OnNext(SaveSlotVM value)
        {
            if (value != null)
            {
                _lobbyInfoController.SaveSlotSelected(value);
                return;
            }
        }

        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }
    }
}
