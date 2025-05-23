using System;
using System.Collections.Generic;
using Kingmaker;
using Kingmaker.UI;
using Kingmaker.UI.Common;
using Kingmaker.UI.MVVM._PCView.SaveLoad;
using Kingmaker.UI.MVVM._PCView.Settings.Entities;
using Kingmaker.UI.ServiceWindow.Credits;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WOTRMultiplayer.Extensions;

namespace WOTRMultiplayer
{
    public class UIElementFactory
    {
        public const string DropdownGameObjectName = "Dropdown";

        private GameObject _dropdownPrefab;
        private SaveLoadPCView _saveLoadPCView;
        private GameObject _defaultGameObject;
        private GameObject _topDecoration;
        private GameObject _bottomDecoration;
        private DefaultMesh _defaultTextMesh;
        private readonly object _actionLock = new();

        public void StoreDropdownPrefab(SettingsEntityDropdownPCView view)
        {
            if (view == null)
            {
                return;
            }

            if (_dropdownPrefab == null)
            {
                lock (_actionLock)
                {
                    if (_dropdownPrefab == null)
                    {
                        Logging.Logger.Info($"Storing {nameof(SettingsEntityDropdownPCView)} prefab");

                        _dropdownPrefab = UnityEngine.Object.Instantiate(view.gameObject);
                        UnityEngine.Object.DontDestroyOnLoad(_dropdownPrefab);
                        UnityEngine.Object.DestroyImmediate(_dropdownPrefab.GetComponent<SettingsEntityDropdownPCView>());
                        UnityEngine.Object.DestroyImmediate(_dropdownPrefab.GetComponent<ContentSizeFitterExtended>());
                        UnityEngine.Object.DestroyImmediate(_dropdownPrefab.GetComponent<VerticalLayoutGroupWorkaround>());
                        UnityEngine.Object.DestroyImmediate(_dropdownPrefab.GetComponent<LayoutElement>());
                    }
                }
            }
        }

        public void StoreSaveLoadPCViewPrefab(SaveLoadPCView view)
        {
            if (view == null)
            {
                return;
            }

            if (_saveLoadPCView == null)
            {
                Logging.Logger.Info($"Storing {nameof(SaveLoadPCView)} prefab");
                _saveLoadPCView = UnityEngine.Object.Instantiate(view);
                UnityEngine.Object.DontDestroyOnLoad(_saveLoadPCView);
            }
        }

        public void StoreDefaultGameObject(GameObject gameObject)
        {
            if (_defaultGameObject == null)
            {
                Logging.Logger.Info($"Storing default prefab");
                _defaultGameObject = UnityEngine.Object.Instantiate(gameObject);
                UnityEngine.Object.DestroyImmediate(_defaultGameObject.GetComponent<UnityEngine.UI.Image>());
                UnityEngine.Object.DontDestroyOnLoad(_defaultGameObject);
            }
        }

        public void StoreTopDecoration(GameObject gameObject)
        {
            if (_topDecoration == null)
            {
                Logging.Logger.Info($"Storing top decoration prefab");
                _topDecoration = UnityEngine.Object.Instantiate(gameObject);
                UnityEngine.Object.DontDestroyOnLoad(_topDecoration);
            }
        }

        public void StoreBottomDecoration(GameObject gameObject)
        {
            if (_bottomDecoration == null)
            {
                Logging.Logger.Info($"Storing bottom decoration prefab");
                _bottomDecoration = UnityEngine.Object.Instantiate(gameObject);
                UnityEngine.Object.DontDestroyOnLoad(_bottomDecoration);
            }
        }

        public GameObject CreateDefaultGameObject(Transform parent)
        {
            return UnityEngine.Object.Instantiate(_defaultGameObject, parent);
        }

        public SaveLoadPCView CreateSaveLoadPCView(Transform parent)
        {
            var saveLoadView = UnityEngine.Object.Instantiate(_saveLoadPCView, parent);
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

            return saveLoadView;
        }

        public GameObject CreateDropdown(float sizeDeltaX, Transform parent)
        {
            var dropdownContainerObject = UnityEngine.Object.Instantiate(_dropdownPrefab, parent);
            dropdownContainerObject.CleanupAllChildren(x => x.name != DropdownGameObjectName);
            var dropdownObject = dropdownContainerObject.transform.Find(DropdownGameObjectName);
            var rect = dropdownObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(sizeDeltaX, rect.sizeDelta.y);
            var dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
            var templateRect = dropdownObject.Find("Template").GetComponent<RectTransform>();
            templateRect.sizeDelta = new Vector2(Math.Abs(templateRect.sizeDelta.x * 5), templateRect.sizeDelta.y);
            dropdown.ClearOptions();

            return dropdownContainerObject;
        }

        public GameObject CreateCopyOfCreditsScreen()
        {
            var copy = UnityEngine.Object.Instantiate(Game.Instance.UI.CreditsUI.gameObject, Game.Instance.UI.MainMenu.transform);
            var originalWindow = copy.GetComponent<CreditsUIWindow>();
            UnityEngine.Object.DestroyImmediate(originalWindow);
            return copy;
        }

        private GameObject CreateTopDecoration(Transform parent)
        {
            return UnityEngine.Object.Instantiate(_topDecoration, parent);
        }

        private GameObject CreateBottomDecoration(Transform parent)
        {
            return UnityEngine.Object.Instantiate(_bottomDecoration, parent);
        }

        public GameObject CreateLobbyWindowContent(Transform parent)
        {
            var lobbyContent = CreateDefaultGameObject(parent);
            lobbyContent.name = "LobbyScreen";
            lobbyContent.CleanupAllChildren();
            var background = CreateDefaultGameObject(lobbyContent.transform);
            background.name = "Background";
            CreateTopDecoration(background.transform);
            CreateBottomDecoration(background.transform);
            //playersTitle.horizontalAlignment = HorizontalAlignmentOptions.Center;
            //var backgroundImageToCopy = this.gameObject.transform.Find("BackgroundGroup").Find("PapperBackgroundImage").gameObject;
            //var backgroundImage = Instantiate(backgroundImageToCopy.gameObject, background.transform);
            //var backgroundImageRectangle = backgroundImage.GetComponent<RectTransform>();
            //backgroundImageRectangle.sizeDelta = lobbyWindowObjectRect.sizeDelta;

            // TBD test info
            var players = new List<string> { "Cat", "Dog", "Squirrel", "Rat" };
            var verticalContent = CreateDefaultGameObject(lobbyContent.transform);
            verticalContent.name = "VerticalContent";
            var vertical = verticalContent.AddComponent<VerticalLayoutGroupWorkaround>();
            var playersTitleObject = CreateDefaultGameObject(verticalContent.transform);
            var playersTitle = playersTitleObject.AddComponent<TextMeshProUGUI>();
            playersTitle.material = _defaultTextMesh.Material;
            playersTitle.color = _defaultTextMesh.Color;
            playersTitle.horizontalAlignment = HorizontalAlignmentOptions.Center;
            playersTitle.SetText("Players");

            foreach (var playerName in players)
            {
                var playerInfoObject = CreateDefaultGameObject(verticalContent.transform);
                playerInfoObject.AddComponent<HorizontalLayoutGroupWorkaround>();
                var ele = playerInfoObject.AddComponent<LayoutElement>();
                var cz = playerInfoObject.AddComponent<ContentSizeFitterExtended>();

                var player = CreateDefaultGameObject(playerInfoObject.transform);
                var playerTitle = player.AddComponent<TextMeshProUGUI>();
                playerTitle.material = _defaultTextMesh.Material;
                playerTitle.color = _defaultTextMesh.Color;
                playerTitle.SetText(playerName);
            }

            var characterControlTitleObject = CreateDefaultGameObject(verticalContent.transform);
            var characterControlTitle = characterControlTitleObject.AddComponent<TextMeshProUGUI>();
            characterControlTitle.material = _defaultTextMesh.Material;
            characterControlTitle.color = _defaultTextMesh.Color;
            characterControlTitle.horizontalAlignment = HorizontalAlignmentOptions.Center;
            characterControlTitle.SetText("Characters");


            var characterInfoLayout = CreateDefaultGameObject(verticalContent.transform);
            var h = characterInfoLayout.AddComponent<HorizontalLayoutGroupWorkaround>();
            h.childAlignment = TextAnchor.MiddleCenter;
            for (int i = 1; i <= 6; i++)
            {
                var specificCharInfo = CreateDefaultGameObject(characterInfoLayout.transform);
                specificCharInfo.AddComponent<VerticalLayoutGroupWorkaround>();

                var characterPortrait = CreateDefaultGameObject(specificCharInfo.transform);
                var portrait = characterPortrait.AddComponent<TextMeshProUGUI>();
                portrait.material = _defaultTextMesh.Material;
                portrait.color = _defaultTextMesh.Color;
                portrait.SetText($"portrait");

                var dropdownContainerObject = Main.Multiplayer.ElementFactory.CreateDropdown(parent.gameObject.GetComponent<RectTransform>().sizeDelta.x / 6, specificCharInfo.transform);
                var dropdownObject = dropdownContainerObject.transform.Find(UIElementFactory.DropdownGameObjectName);
                var dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
                dropdown.AddOptions(players);
                dropdown.onValueChanged.AddListener(OnValueChanged);
            }

            return lobbyContent;
        }

        private void OnValueChanged(int arg0)
        {
            Logging.Logger.Info($"Value changed. Index={arg0}");
        }

        public void StoreDefaultTextMesh(TextMeshProUGUI defaultTextMesh)
        {
            if (_defaultTextMesh == null)
            {
                _defaultTextMesh = new DefaultMesh
                {
                    Color = defaultTextMesh.color,
                    Material = defaultTextMesh.material,
                };
            }
        }

        private class DefaultMesh
        {
            public Material Material { get; set; }

            public Color Color { get; set; }
        }
    }
}
