using System;
using Kingmaker.UI.MVVM._PCView.SaveLoad;
using Kingmaker.UI.MVVM._PCView.Settings.Entities;
using Kingmaker.UI.MVVM._VM.Settings;
using Owlcat.Runtime.UI.VirtualListSystem;
using TMPro;
using UnityEngine;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Windows;
using WOTRMultiplayer.Entities;

namespace WOTRMultiplayer.Abstractions.UI
{
    public interface IUIFactory
    {
        WOTRMultiplayer.UI.Mesh DefaultTextMesh { get; }

        IMultiplayerWindow InitializeMultiplayerWindow(InitializeMultiplayerContext context, Action onShow);

        GameObject CreateBorderDecoration(Transform parent);

        GameObject CreateDefaultGameObject(Transform parent);

        GameObject CreateDropdown(float preferedWidth, Transform parent);

        GameObject CreateButton(Transform parent);

        GameObject CreateInput(Transform parent);

        GameObject CreateLobbyWindowContent(Transform parent);

        SaveLoadPCView CreateSaveLoadPCView(Transform parent);

        ILobbyWindow InitializeEscMenuLobbyWindow(ILobbyWindowController lobbyWindowController, Action onShow);

        GameObject CreateBackgroundArt(Transform parent);

        void StoreBorderDecoration(GameObject gameObject);

        void StoreDefaultGameObject(GameObject gameObject);

        void StoreDefaultTextMesh(TextMeshProUGUI defaultTextMesh);

        void StoreDropdownPrefab(SettingsEntityDropdownPCView view);

        void StoreInputPrefab(GameObject inputObject);

        void StoreButtonPrefab(GameObject buttonObject);

        void StoreSaveLoadPCViewPrefab(SaveLoadPCView view);

        void StoreBackgroundArt(GameObject backgroundArt);

        void DestroyLobbyWindow(ILobbyWindow lobbyWindow);

        void PopulateMultiplayerSettingsUI(SettingsVM instance);

        IVirtualListElementView InitializeInputSettingTemplate(GameObject settingPrefab);

        void CreateMultiplayerSettingsMenu(SettingsVM settingsVM);

        void StoreCloseButtonPrefab(GameObject closeButtonObject);

        GameObject CreateCloseButton(Transform parent);
    }
}
