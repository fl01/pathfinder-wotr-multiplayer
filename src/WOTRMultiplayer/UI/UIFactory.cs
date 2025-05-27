using System;
using Kingmaker;
using Kingmaker.UI;
using Kingmaker.UI.Common;
using Kingmaker.UI.MVVM._PCView.SaveLoad;
using Kingmaker.UI.MVVM._PCView.Settings.Entities;
using Kingmaker.UI.ServiceWindow.Credits;
using Owlcat.Runtime.UI.Controls.Button;
using Serilog;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.UI.Lobby;
using WOTRMultiplayer.Unity;

namespace WOTRMultiplayer.UI
{
    public class UIFactory
    {
        public const string DropdownGameObjectName = "Dropdown";
        public const int LobbySectionTitleHeight = 50;
        private GameObject _dropdownPrefab;
        private SaveLoadPCView _saveLoadPCView;
        private GameObject _defaultGameObject;
        private GameObject _borderDecoration;
        private DefaultMesh _defaultTextMesh;
        private readonly object _actionLock = new();

        public static int GetMaxCharactersCount()
        {
            // should be const, but it might need to be modified via harmony for some other mods
            return 6;
        }

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
                        Log.Logger.Information("Storing {prefabTypeName} prefab", nameof(SettingsEntityDropdownPCView));

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
                lock (_actionLock)
                {
                    if (_saveLoadPCView == null)
                    {
                        Log.Logger.Information("Storing {prefabTypeName} prefab", nameof(SaveLoadPCView));
                        _saveLoadPCView = UnityEngine.Object.Instantiate(view);
                        UnityEngine.Object.DontDestroyOnLoad(_saveLoadPCView);
                    }
                }
            }
        }

        public void StoreDefaultGameObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            if (_defaultGameObject == null)
            {
                lock (_actionLock)
                {
                    if (_defaultGameObject == null)
                    {
                        Log.Logger.Information("Storing default prefab");
                        _defaultGameObject = UnityEngine.Object.Instantiate(gameObject);
                        UnityEngine.Object.DestroyImmediate(_defaultGameObject.GetComponent<Image>());
                        UnityEngine.Object.DontDestroyOnLoad(_defaultGameObject);
                    }
                }
            }
        }

        public void StoreBorderDecoration(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            if (_borderDecoration == null)
            {
                lock (_actionLock)
                {
                    if (_borderDecoration == null)
                    {
                        Log.Logger.Information("Storing border decoration prefab");
                        _borderDecoration = UnityEngine.Object.Instantiate(gameObject);
                        UnityEngine.Object.DontDestroyOnLoad(_borderDecoration);
                    }
                }
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
            buttonCopy2.GetComponent<OwlcatButton>().Interactable = false;
            buttonCopy2.name = "Button2";
            var buttonCopy3 = UnityEngine.Object.Instantiate(baseButton, buttons);
            buttonCopy3.GetComponent<OwlcatButton>().Interactable = false;
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

        //private GameObject CreateTopDecoration(Transform parent)
        //{
        //    return UnityEngine.Object.Instantiate(_topDecoration, parent);
        //}

        private GameObject CreateBorderDecoration(Transform parent)
        {
            return UnityEngine.Object.Instantiate(_borderDecoration, parent);
        }

        public GameObject CreateLobbyWindowContent(Transform parent)
        {
            var lobbyContent = CreateDefaultGameObject(parent);
            lobbyContent.name = LobbyInfoController.LobbyScreenRootObjectName;
            lobbyContent.CleanupAllChildren();
            var background = CreateDefaultGameObject(lobbyContent.transform);
            background.name = "Background";
            //CreateTopDecoration(background.transform);
            CreateBorderDecoration(background.transform);
            //playersTitle.horizontalAlignment = HorizontalAlignmentOptions.Center;
            //var backgroundImageToCopy = this.gameObject.transform.Find("BackgroundGroup").Find("PapperBackgroundImage").gameObject;
            //var backgroundImage = Instantiate(backgroundImageToCopy.gameObject, background.transform);
            //var backgroundImageRectangle = backgroundImage.GetComponent<RectTransform>();
            //backgroundImageRectangle.sizeDelta = lobbyWindowObjectRect.sizeDelta;

            var verticalContent = CreateDefaultGameObject(lobbyContent.transform);
            verticalContent.name = LobbyInfoController.LobbyContentObjectName;
            var rootVertical = verticalContent.AddComponent<VerticalLayoutGroup>();
            rootVertical.padding = new RectOffset(0, 0, 0, 20);
            CreateLobbyPlayersSection(verticalContent.transform);

            var width = parent.GetComponent<RectTransform>().sizeDelta.x;
            CreateLobbyCharactersSection(width, verticalContent.transform);

            return lobbyContent;
        }

        private void CreateLobbyPlayersSection(Transform parent)
        {
            var playersSectionObject = CreateDefaultGameObject(parent);
            var playersSectionRect = playersSectionObject.GetComponent<RectTransform>();
            playersSectionRect.pivot = new Vector2(0.5f, 1f); // upper center
            playersSectionObject.name = LobbyInfoController.PlayersSectionObjectName;
            playersSectionObject.AddComponent<VerticalLayoutGroup>();
            var playersSectionSizeFitter = playersSectionObject.AddComponent<ContentSizeFitter>();
            playersSectionSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var playersTitleObject = CreateDefaultGameObject(playersSectionObject.transform);
            playersTitleObject.name = LobbyInfoController.PlayersSectionObjectName;
            var playersTitle = playersTitleObject.AddComponent<TextMeshProUGUI>();
            var playersTitleLayout = playersTitleObject.AddComponent<LayoutElement>();
            playersTitleLayout.preferredHeight = LobbySectionTitleHeight;
            playersTitle.material = _defaultTextMesh.Material;
            playersTitle.color = _defaultTextMesh.Color;
            playersTitle.horizontalAlignment = HorizontalAlignmentOptions.Center;
            playersTitle.SetText(UIUtility.GetSaberBookFormat(StringConsts.LobbyInfoWindow.PlayersSectionTitle));

            var playersSectionContentObject = CreateDefaultGameObject(playersSectionObject.transform);
            playersSectionContentObject.name = LobbyInfoController.PlayersSectionContentObjectName;
            playersSectionContentObject.AddComponent<VerticalLayoutGroup>();
        }

        private void CreateLobbyCharactersSection(float width, Transform parent)
        {
            var charactersSectionObject = CreateDefaultGameObject(parent);
            var charactersSectionRect = charactersSectionObject.GetComponent<RectTransform>();
            charactersSectionRect.pivot = new Vector2(0.5f, 0f); // bottom (almost) center
            charactersSectionObject.name = LobbyInfoController.CharactersSectionObjectName;
            charactersSectionObject.AddComponent<VerticalLayoutGroup>();
            var charactersSectionSizeFitter = charactersSectionObject.AddComponent<ContentSizeFitter>();
            charactersSectionSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var charactersSectionTitleObject = CreateDefaultGameObject(charactersSectionObject.transform);
            var charactersSectionTitleLayout = charactersSectionTitleObject.AddComponent<LayoutElement>();
            charactersSectionTitleLayout.preferredHeight = LobbySectionTitleHeight;
            charactersSectionTitleObject.name = LobbyInfoController.CharactersSectionTitleObjectName;
            var characterControlTitle = charactersSectionTitleObject.AddComponent<TextMeshProUGUI>();
            characterControlTitle.material = _defaultTextMesh.Material;
            characterControlTitle.color = _defaultTextMesh.Color;
            characterControlTitle.horizontalAlignment = HorizontalAlignmentOptions.Center;
            characterControlTitle.SetText(UIUtility.GetSaberBookFormat(StringConsts.LobbyInfoWindow.CharactersSectionTitle));
            var charactersSectionContentObject = CreateDefaultGameObject(charactersSectionObject.transform);
            charactersSectionContentObject.name = LobbyInfoController.CharactersSectionContentObjectName;
            charactersSectionContentObject.AddComponent<HorizontalLayoutGroup>();
            var preferedWidth = width / GetMaxCharactersCount();
            for (int characterIndex = 0; characterIndex < GetMaxCharactersCount(); characterIndex++)
            {
                var characterObject = CreateDefaultGameObject(charactersSectionContentObject.transform);
                characterObject.name = LobbyInfoController.CharacterContainerObjectName;
                characterObject.AddComponent<VerticalLayoutGroup>();
                characterObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                var characterPortrait = CreateDefaultGameObject(characterObject.transform);
                characterPortrait.name = LobbyInfoController.CharacterPortraitObjectName;
                characterPortrait.AddComponent<Image>();
                var portraitLayoutElement = characterPortrait.AddComponent<LayoutElement>();
                portraitLayoutElement.preferredWidth = preferedWidth;
                portraitLayoutElement.preferredHeight = preferedWidth * 1.2f;

                var dropdownContainerObject = Main.Multiplayer.Factory.CreateDropdown(preferedWidth, characterObject.transform);
                var characterIndexComponent = dropdownContainerObject.AddComponent<CharacterIndexMonoBehavior>();
                characterIndexComponent.CharacterIndex = characterIndex;
                dropdownContainerObject.name = LobbyInfoController.CharacterOwnerObjectName;
            }
        }

        public void StoreDefaultTextMesh(TextMeshProUGUI defaultTextMesh)
        {
            _defaultTextMesh ??= new DefaultMesh
            {
                Color = defaultTextMesh.color,
                Material = defaultTextMesh.material,
            };
        }

        public DefaultMesh GetDefaultMesh()
        {
            return _defaultTextMesh;
        }
    }
}
