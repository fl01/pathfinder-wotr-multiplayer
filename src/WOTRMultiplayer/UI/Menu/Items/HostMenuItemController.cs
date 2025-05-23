using System;
using Kingmaker.UI.MVVM;
using Kingmaker.UI.MVVM._PCView.SaveLoad;
using Kingmaker.UI.MVVM._VM.SaveLoad;
using Kingmaker.UI.MVVM._VM.ServiceWindows.CharacterInfo.Sections.Abilities;
using Owlcat.Runtime.UI.VirtualListSystem;
using TMPro;
using UnityEngine;
using WOTRMultiplayer.Strings;

namespace WOTRMultiplayer.UI.Menu.Items
{
    public class HostMenuItemController : MenuItemController, IObserver<SaveSlotVM>
    {
        private SaveLoadVM _saveLoadViewModel;
        private bool _setupLayout = true;

        public HostMenuItemController(MultiplayerWindow multiplayerWindow, GameObject menuItem, GameObject menuContent)
            : base(multiplayerWindow, menuItem, menuContent)
        {
            var label = menuItem.GetComponentInChildren<TextMeshProUGUI>();
            label.SetText(StringConsts.MultiplayerWindow.HostMenuLabel);
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
                Logging.Logger.Info($"Selected SaveSlo={value.SaveName.Value}");
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
