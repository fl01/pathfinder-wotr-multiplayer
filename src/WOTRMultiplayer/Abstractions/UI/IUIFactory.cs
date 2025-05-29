using Kingmaker.UI.MVVM._PCView.SaveLoad;
using Kingmaker.UI.MVVM._PCView.Settings.Entities;
using TMPro;
using UnityEngine;

namespace WOTRMultiplayer.Abstractions.UI
{
    public interface IUIFactory
    {
        GameObject CreateCopyOfCreditsScreen();
        GameObject CreateDefaultGameObject(Transform parent);
        GameObject CreateDropdown(float preferedWidth, Transform parent);
        GameObject CreateButton(Transform transform);
        GameObject CreateInput(Transform transform);
        GameObject CreateLobbyWindowContent(Transform parent, bool interactableDropdown);
        SaveLoadPCView CreateSaveLoadPCView(Transform parent);
        void CreateBackgroundArt(Transform parent);
        WOTRMultiplayer.UI.Mesh GetDefaultMesh();
        void StoreBorderDecoration(GameObject gameObject);
        void StoreDefaultGameObject(GameObject gameObject);
        void StoreDefaultTextMesh(TextMeshProUGUI defaultTextMesh);
        void StoreDropdownPrefab(SettingsEntityDropdownPCView view);
        void StoreInputPrefab(GameObject inputObject);
        void StoreButtonPrefab(GameObject buttonObject);

        void StoreSaveLoadPCViewPrefab(SaveLoadPCView view);
        void StoreBackgroundArt(GameObject gameObject);
    }
}
