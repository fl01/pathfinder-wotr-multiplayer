using Kingmaker.UI.MVVM._PCView.SaveLoad;
using Kingmaker.UI.MVVM._PCView.Settings.Entities;
using Kingmaker.UI.MVVM._VM.Settings;
using Owlcat.Runtime.UI.VirtualListSystem;
using TMPro;
using UnityEngine;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Windows;

namespace WOTRMultiplayer.Abstractions.UI
{
    public interface IUIFactory
    {
        WOTRMultiplayer.UI.Mesh DefaultTextMesh { get; }

        void InitializeMultiplayerWindow();

        GameObject CreateProgressBar(Transform parent, int size, float thickness, bool withBackground = false);

        Sprite CreateRingSprite(int size = 256, float thickness = 0.25f);

        GameObject CreateBorderDecoration(Transform parent);

        GameObject CreateDefaultGameObject(Transform parent);

        GameObject CreateDropdown(float preferredWidth, Transform parent);

        GameObject CreateButton(Transform parent);

        GameObject CreateInput(Transform parent);

        GameObject CreateLobbyWindowContent(Transform parent);

        SaveLoadPCView CreateSaveLoadPCView(Transform parent);

        ILobbyWindow InitializeEscMenuLobbyWindow(ILobbyWindowController lobbyWindowController);

        GameObject CreateBackgroundArt(Transform parent);

        void StoreBorderDecoration(GameObject gameObject);

        void StoreDefaultGameObject(GameObject gameObject);

        void StoreDefaultTextMesh(TextMeshProUGUI defaultTextMesh);

        void StoreDropdownPrefab(SettingsEntityDropdownPCView view);

        void StoreInputPrefab(GameObject inputObject);

        void StoreButtonPrefab(GameObject buttonObject);

        void StoreSaveLoadPCViewPrefab(SaveLoadPCView view);

        void StoreBackgroundArt(GameObject backgroundArt);

        void PopulateMultiplayerSettingsUI(SettingsVM instance);

        IVirtualListElementView InitializeInputSettingTemplate(GameObject settingPrefab);

        void CreateMultiplayerSettingsMenu(SettingsVM settingsVM);

        void StoreCloseButtonPrefab(GameObject closeButtonObject);

        GameObject CreateCloseButton(Transform parent);

        GameObject CreateIconButton(Transform parent, Sprite defaultSprite, Sprite hoverSprite = null, Sprite pressedSprite = null);

        void DestroyStandaloneLobbyWindow();
    }
}
