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

            // it's important to be called inbetween
            saveLoad.Bind(_saveLoadViewModel);
            _saveLoadViewModel.SelectedSaveSlot.Subscribe(this);
            if (_setupLayout)
            {
                CleanupLoadSaveGamesLayout(saveLoad);
            }

            _setupLayout = false;
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

        private void CleanupLoadSaveGamesLayout(SaveLoadPCView saveLoadView)
        {
            UnityEngine.Object.DestroyImmediate(saveLoadView.gameObject.transform.Find("BackgroundWorldCover").gameObject);
            UnityEngine.Object.DestroyImmediate(saveLoadView.gameObject.transform.Find("Background").gameObject);
            var screen = saveLoadView.gameObject.transform.Find("SaveLoadScreen");
            var top = screen.Find("Top");
            UnityEngine.Object.DestroyImmediate(top.gameObject);

            var saveLoadDetails = screen.Find("SaveLoadDetails");
            var picture = saveLoadDetails.Find("Picture");
            UnityEngine.Object.DestroyImmediate(picture.gameObject);

            var info = saveLoadDetails.Find("Info");
            info.gameObject.CleanupAllChildren(x => x.name != "Buttons");
            var buttons = info.Find("Buttons");
            // TBD random buttons as placeholders
            var baseButton = buttons.Find("OwlcatButton").gameObject;
            var layout = baseButton.GetComponent<RectTransform>();
            layout.sizeDelta = new Vector2(layout.sizeDelta.x * 0.92f, layout.sizeDelta.y);
            var buttonCopy1 = UnityEngine.Object.Instantiate(baseButton, buttons);
            buttonCopy1.name = "Button1";
            var buttonCopy2 = UnityEngine.Object.Instantiate(baseButton, buttons);
            buttonCopy2.name = "Button2";
            var buttonCopy3 = UnityEngine.Object.Instantiate(baseButton, buttons);
            buttonCopy3.name = "Button3";
            buttons.gameObject.CleanupAllChildren(
                x => x.name != "DlcRequiredLabel" && x.name != buttonCopy1.name && x.name != buttonCopy2.name && x.name != buttonCopy3.name);
            buttonCopy1.GetComponentInChildren<TextMeshProUGUI>().text = "Host";
            buttonCopy2.GetComponentInChildren<TextMeshProUGUI>().text = "Ready";
            buttonCopy3.GetComponentInChildren<TextMeshProUGUI>().text = "Start";
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
