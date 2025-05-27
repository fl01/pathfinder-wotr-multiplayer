using Kingmaker.UI.MVVM._PCView.SaveLoad;
using Kingmaker.UI.MVVM._PCView.Settings.Entities;
using TMPro;
using UnityEngine;
using WOTRMultiplayer.UI;

namespace WOTRMultiplayer.Abstractions.UI
{
    public interface IUIFactory
    {
        GameObject CreateCopyOfCreditsScreen();
        GameObject CreateDefaultGameObject(Transform parent);
        GameObject CreateDropdown(float preferedWidth, Transform parent);
        GameObject CreateLobbyWindowContent(Transform parent);
        SaveLoadPCView CreateSaveLoadPCView(Transform parent);
        WOTRMultiplayer.UI.Mesh GetDefaultMesh();
        void StoreBorderDecoration(GameObject gameObject);
        void StoreDefaultGameObject(GameObject gameObject);
        void StoreDefaultTextMesh(TextMeshProUGUI defaultTextMesh);
        void StoreDropdownPrefab(SettingsEntityDropdownPCView view);
        void StoreSaveLoadPCViewPrefab(SaveLoadPCView view);
    }
}
