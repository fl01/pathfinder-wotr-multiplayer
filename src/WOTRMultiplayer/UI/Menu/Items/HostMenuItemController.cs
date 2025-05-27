using System;
using Kingmaker.UI.MVVM;
using Kingmaker.UI.MVVM._PCView.SaveLoad;
using Kingmaker.UI.MVVM._VM.SaveLoad;
using Kingmaker.UI.MVVM._VM.ServiceWindows.CharacterInfo.Sections.Abilities;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.VirtualListSystem;
using TMPro;
using UnityEngine;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Controllers.Menu;
using WOTRMultiplayer.Extensions;

namespace WOTRMultiplayer.UI.Menu.Items
{
    public class HostMenuItemController : MenuItemController, IHostMenuItemController, IObserver<SaveSlotVM>
    {
        public const string HostMenuItemContentObjectName = "HostMenuItemContent";
        private readonly ILogger<HostMenuItemController> _logger;
        private readonly ILobbyWindowController _lobbyWindowController;

        private SaveLoadVM _saveLoadViewModel;
        private GameObject _menuContent;

        protected override GameObject MenuContent => _menuContent;

        public HostMenuItemController(
            ILogger<HostMenuItemController> logger,
            ILobbyWindowController lobbyWindowController)
            : base(logger)
        {
            _logger = logger;
            _lobbyWindowController = lobbyWindowController;
        }

        public override void Activate()
        {
            _logger.LogInformation("Trying to activate");

            if (IsActive)
            {
                return;
            }

            var saveLoad = _menuContent.transform.GetChild(0).GetComponent<SaveLoadPCView>();
            _saveLoadViewModel = new SaveLoadVM(SaveLoadMode.Load, true, OnCloseSaveLoadVM, RootUIContext.Instance.CommonVM);

            if (SetupLayout)
            {
                SetupLayout = false;
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
            var saveLoadView = _menuContent.transform.GetChild(0);
            var screen = saveLoadView.gameObject.transform.Find("SaveLoadScreen");
            var container = screen.Find("SaveLoadDetails");
            var parentContainerRect = container.GetComponent<RectTransform>();

            var lobbyWindowObject = UnityEngine.Object.Instantiate(baseLayout, container.transform);
            lobbyWindowObject.name = "MultiplayerLobby";
            lobbyWindowObject.CleanupAllChildren();
            var title = container.Find("Title");
            var lobbyWindowObjectPosition = new Vector3(title.position.x, lobbyWindowObject.transform.position.y * 1.1f, lobbyWindowObject.transform.position.z);
            lobbyWindowObject.transform.SetPositionAndRotation(lobbyWindowObjectPosition, lobbyWindowObject.transform.rotation);
            var lobbyWindowObjectRect = lobbyWindowObject.GetComponent<RectTransform>();
            lobbyWindowObjectRect.sizeDelta = new Vector2(parentContainerRect.sizeDelta.x * 0.9f, parentContainerRect.sizeDelta.y * 0.72f);

            _lobbyWindowController.InitializeContent(lobbyWindowObject.transform);
        }

        private void SetupLoadSaveGamesLayout()
        {
            SaveLoadPCView saveLoad = Main.Multiplayer.Factory.CreateSaveLoadPCView(_menuContent.transform);
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
                _lobbyWindowController.SaveSlotSelected(value);
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
