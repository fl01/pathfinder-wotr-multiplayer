using System;
using Kingmaker;
using Kingmaker.Localization;
using Kingmaker.UI;
using Kingmaker.UI.Common;
using Kingmaker.UI.MVVM._PCView.EscMenu;
using Kingmaker.UI.MVVM._PCView.SaveLoad;
using Kingmaker.UI.MVVM._PCView.Settings.Entities;
using Kingmaker.UI.ServiceWindow.Credits;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.Controls.Button;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.UI.Lobby;
using WOTRMultiplayer.UI.Menu.Items;
using WOTRMultiplayer.Unity.Behaviours;

namespace WOTRMultiplayer.UI
{
    public class UIFactory : IUIFactory
    {
        public const string DropdownGameObjectName = "Dropdown";
        public const string InputPlaceholderObjectName = "PlaceholderText";
        public const string InputLabelObjectName = "Label_Input";
        public const string MultiplayerMenuObjectName = "MultiplayerLobbyButton";

        public const int LobbySectionTitleHeight = 50;
        private GameObject _dropdownPrefab;
        private GameObject _inputPrefab;
        private GameObject _buttonPrefab;
        private GameObject _backgroundArtPrefab;
        private SaveLoadPCView _saveLoadPCView;
        private GameObject _defaultGameObject;
        private GameObject _borderDecoration;
        private Mesh _defaultTextMesh;
        private readonly object _actionLock = new();
        private readonly ILogger<UIFactory> _logger;

        public UIFactory(ILogger<UIFactory> logger)
        {
            _logger = logger;
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
                        _logger.LogInformation("Storing {prefabTypeName} prefab", nameof(SettingsEntityDropdownPCView));

                        _dropdownPrefab = UnityEngine.Object.Instantiate(view.gameObject);
                        UnityEngine.Object.DestroyImmediate(_dropdownPrefab.GetComponent<SettingsEntityDropdownPCView>());
                        UnityEngine.Object.DestroyImmediate(_dropdownPrefab.GetComponent<ContentSizeFitterExtended>());
                        UnityEngine.Object.DestroyImmediate(_dropdownPrefab.GetComponent<VerticalLayoutGroupWorkaround>());
                        UnityEngine.Object.DestroyImmediate(_dropdownPrefab.GetComponent<LayoutElement>());
                        UnityEngine.Object.DontDestroyOnLoad(_dropdownPrefab);
                    }
                }
            }
        }

        public void StoreSaveLoadPCViewPrefab(SaveLoadPCView view)
        {
            if (view == null)
            {
                _logger.LogWarning("SaveLoadPCView is null");
                return;
            }

            if (_saveLoadPCView == null)
            {
                lock (_actionLock)
                {
                    if (_saveLoadPCView == null)
                    {
                        _logger.LogInformation("Storing {prefabTypeName} prefab", nameof(SaveLoadPCView));
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
                        _logger.LogInformation("Storing default prefab");
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
                        _logger.LogInformation("Storing border decoration prefab");
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
            saveLoadView.name = HostMenuItemController.SaveLoadView;
            UnityEngine.Object.DestroyImmediate(saveLoadView.gameObject.transform.Find("BackgroundWorldCover").gameObject);
            UnityEngine.Object.DestroyImmediate(saveLoadView.gameObject.transform.Find("Background").gameObject);
            var screen = saveLoadView.gameObject.transform.Find(HostMenuItemController.SaveLoadScreen);
            UnityEngine.Object.DestroyImmediate(screen.Find("PapperBackground").gameObject);
            var top = screen.Find("Top");
            UnityEngine.Object.DestroyImmediate(top.gameObject);

            var saveLoadDetails = screen.Find(HostMenuItemController.SaveLoadDetails);
            var picture = saveLoadDetails.Find("Picture");
            UnityEngine.Object.DestroyImmediate(picture.gameObject);
            var info = saveLoadDetails.Find(HostMenuItemController.SaveLoadDetailsInfo);
            info.gameObject.CleanupAllChildren(x => x.name != HostMenuItemController.SaveLoadDetailsInfoButtons);
            var buttons = info.Find(HostMenuItemController.SaveLoadDetailsInfoButtons);

            var baseButton = buttons.Find("OwlcatButton").gameObject;
            var layout = baseButton.GetComponent<RectTransform>();
            layout.sizeDelta = new Vector2(layout.sizeDelta.x * 0.92f, layout.sizeDelta.y);
            var hostButton = UnityEngine.Object.Instantiate(baseButton, buttons);
            hostButton.name = HostMenuItemController.HostButtonObjectName;
            var readyButton = UnityEngine.Object.Instantiate(baseButton, buttons);
            readyButton.name = HostMenuItemController.ReadyButtonObjectName;
            var startButton = UnityEngine.Object.Instantiate(baseButton, buttons);
            startButton.name = HostMenuItemController.StartButtonObjectName;
            buttons.gameObject.CleanupAllChildren(
                x => x.name != "DlcRequiredLabel" && x.name != hostButton.name && x.name != readyButton.name && x.name != startButton.name);

            return saveLoadView;
        }

        public GameObject CreateButton(Transform parent)
        {
            var buttonObject = UnityEngine.Object.Instantiate(_buttonPrefab, parent);
            return buttonObject;
        }

        public GameObject CreateInput(Transform parent)
        {
            var inputObject = UnityEngine.Object.Instantiate(_inputPrefab, parent);
            return inputObject;
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

        private GameObject CreateBorderDecoration(Transform parent)
        {
            return UnityEngine.Object.Instantiate(_borderDecoration, parent);
        }

        public GameObject CreateLobbyWindowContent(Transform parent, bool interactableDropdown)
        {
            var lobbyContent = CreateDefaultGameObject(parent);
            lobbyContent.name = LobbyWindowController.LobbyScreenRootObjectName;
            lobbyContent.CleanupAllChildren();
            var background = CreateDefaultGameObject(lobbyContent.transform);
            background.name = "Background";
            CreateBorderDecoration(background.transform);

            var verticalContent = CreateDefaultGameObject(lobbyContent.transform);
            verticalContent.name = LobbyWindowController.LobbyContentObjectName;
            var rootVertical = verticalContent.AddComponent<VerticalLayoutGroup>();
            rootVertical.padding = new RectOffset(0, 0, 0, 20);
            CreateLobbyServerInfoSection(verticalContent.transform);

            CreateLobbyPlayersSection(verticalContent.transform);

            var width = parent.GetComponent<RectTransform>().sizeDelta.x;
            CreateLobbyCharactersSection(width, verticalContent.transform, interactableDropdown);

            return lobbyContent;
        }

        public (GameObject menuItem, GameObject windowContainer) CreateEscMenuItem(EscMenuPCView view)
        {
            _logger.LogInformation("Creating MultiplayerMenu");
            var optionsButton = view.transform.Find("Window/ButtonBlock/OptionsButton");
            var multiplayerMenu = UnityEngine.Object.Instantiate(optionsButton.gameObject, optionsButton.transform.parent);
            multiplayerMenu.transform.SetSiblingIndex(optionsButton.GetSiblingIndex());
            multiplayerMenu.name = MultiplayerMenuObjectName;
            var textObject = multiplayerMenu.transform.Find("Text");
            UnityEngine.Object.DestroyImmediate(textObject.GetComponent<LocalizedUIText>());
            textObject.GetComponent<TextMeshProUGUI>().SetText(UIStringConsts.EscMenu.LobbyMenuItemTitle);

            _logger.LogInformation("Window container parent set. GameObjectName={name}", view.transform.parent.gameObject.name);
            var windowContainer = CreateDefaultGameObject(view.transform.parent);
            windowContainer.name = "EscMultiplayerLobbyWindowContainer";
            var windowContainerRect = windowContainer.GetComponent<RectTransform>();
            windowContainerRect.sizeDelta = new Vector2(Screen.width * 0.35f, Screen.height * 0.6f);
            windowContainerRect.anchorMin = new Vector2(0.5f, 0.5f);
            windowContainerRect.anchorMax = new Vector2(0.5f, 0.5f);
            return (multiplayerMenu, windowContainer);
        }

        public void StoreDefaultTextMesh(TextMeshProUGUI defaultTextMesh)
        {
            _defaultTextMesh ??= new Mesh
            {
                Color = defaultTextMesh.color,
                Material = defaultTextMesh.material,
            };
        }

        public Mesh GetDefaultMesh()
        {
            return _defaultTextMesh;
        }

        public void StoreInputPrefab(GameObject inputObject)
        {
            if (inputObject == null)
            {
                return;
            }

            if (_inputPrefab == null)
            {
                lock (_actionLock)
                {
                    if (_inputPrefab == null)
                    {
                        _logger.LogInformation("Storing input prefab");

                        _inputPrefab = UnityEngine.Object.Instantiate(inputObject);
                        var placeHolderTextObject = _inputPrefab.transform.Find(InputPlaceholderObjectName);
                        var localizedTextComponent = placeHolderTextObject.GetComponent<LocalizedUIText>();
                        if (localizedTextComponent != null)
                        {
                            UnityEngine.Object.DestroyImmediate(localizedTextComponent);
                        }
                    }
                }
            }
        }

        public void StoreButtonPrefab(GameObject buttonObject)
        {
            if (buttonObject == null)
            {
                return;
            }

            if (_buttonPrefab == null)
            {
                lock (_actionLock)
                {
                    if (_buttonPrefab == null)
                    {
                        _logger.LogInformation("Storing button prefab");

                        _buttonPrefab = UnityEngine.Object.Instantiate(buttonObject);
                        var rectTransform = _buttonPrefab.GetComponent<RectTransform>();
                        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                        var button = _buttonPrefab.GetComponent<OwlcatButton>();
                        for (var i = 0; i < button.OnLeftClick.GetPersistentEventCount(); i++)
                        {
                            button.OnLeftClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
                        }
                    }
                }
            }
        }

        public void CreateBackgroundArt(Transform parent)
        {
            UnityEngine.Object.Instantiate(_backgroundArtPrefab, parent);
        }

        public void StoreBackgroundArt(GameObject backgroundArt)
        {
            if (backgroundArt == null)
            {
                return;
            }

            if (_backgroundArtPrefab == null)
            {
                lock (_actionLock)
                {
                    if (_backgroundArtPrefab == null)
                    {
                        _logger.LogInformation("Storing background art");

                        _backgroundArtPrefab = UnityEngine.Object.Instantiate(backgroundArt);
                        UnityEngine.Object.DestroyImmediate(_backgroundArtPrefab.GetComponent<Image>());
                        UnityEngine.Object.DontDestroyOnLoad(_backgroundArtPrefab);
                    }
                }
            }
        }

        private void CreateLobbyServerInfoSection(Transform parent)
        {
            var serverInfoSectionObject = CreateDefaultGameObject(parent);
            var serverInfoSectionRect = serverInfoSectionObject.GetComponent<RectTransform>();
            serverInfoSectionRect.pivot = new Vector2(0.5f, 1f); // upper center
            serverInfoSectionObject.name = LobbyWindowController.ServerInfoSectionObjectName;
            serverInfoSectionObject.AddComponent<VerticalLayoutGroup>();
            var serverInfoSectionSizeFitter = serverInfoSectionObject.AddComponent<ContentSizeFitter>();
            serverInfoSectionSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var serverInfoTitleObject = CreateDefaultGameObject(serverInfoSectionObject.transform);
            serverInfoTitleObject.name = LobbyWindowController.ServerInfoSectionTitleObjectName;
            var serverInfoTitle = serverInfoTitleObject.AddComponent<TextMeshProUGUI>();
            var serverInfoTitleLayout = serverInfoTitleObject.AddComponent<LayoutElement>();
            serverInfoTitleLayout.preferredHeight = LobbySectionTitleHeight;
            serverInfoTitle.material = _defaultTextMesh.Material;
            serverInfoTitle.color = _defaultTextMesh.Color;
            serverInfoTitle.horizontalAlignment = HorizontalAlignmentOptions.Center;
            serverInfoTitle.SetText(UIUtility.GetSaberBookFormat(UIStringConsts.LobbyInfoWindow.ServerInfoSectionTitle));

            var serverInfoSectionContentObject = CreateDefaultGameObject(serverInfoSectionObject.transform);
            serverInfoSectionContentObject.name = LobbyWindowController.ServerInfoSectionContentObjectName;
            serverInfoSectionContentObject.AddComponent<VerticalLayoutGroup>();
        }

        private void CreateLobbyPlayersSection(Transform parent)
        {
            var playersSectionObject = CreateDefaultGameObject(parent);
            var playersSectionRect = playersSectionObject.GetComponent<RectTransform>();
            playersSectionRect.pivot = new Vector2(0.5f, 1f); // upper center
            playersSectionObject.name = LobbyWindowController.PlayersSectionObjectName;
            playersSectionObject.AddComponent<VerticalLayoutGroup>();
            var playersSectionSizeFitter = playersSectionObject.AddComponent<ContentSizeFitter>();
            playersSectionSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var playersTitleObject = CreateDefaultGameObject(playersSectionObject.transform);
            playersTitleObject.name = LobbyWindowController.PlayersSectionTitleObjectName;
            var playersTitle = playersTitleObject.AddComponent<TextMeshProUGUI>();
            var playersTitleLayout = playersTitleObject.AddComponent<LayoutElement>();
            playersTitleLayout.preferredHeight = LobbySectionTitleHeight;
            playersTitle.material = _defaultTextMesh.Material;
            playersTitle.color = _defaultTextMesh.Color;
            playersTitle.horizontalAlignment = HorizontalAlignmentOptions.Center;
            playersTitle.SetText(UIUtility.GetSaberBookFormat(UIStringConsts.LobbyInfoWindow.PlayersSectionTitle));

            var playersSectionContentObject = CreateDefaultGameObject(playersSectionObject.transform);
            playersSectionContentObject.name = LobbyWindowController.PlayersSectionContentObjectName;
            playersSectionContentObject.AddComponent<VerticalLayoutGroup>();
        }

        private void CreateLobbyCharactersSection(float width, Transform parent, bool interactableDropdown)
        {
            var charactersSectionObject = CreateDefaultGameObject(parent);
            var charactersSectionRect = charactersSectionObject.GetComponent<RectTransform>();
            charactersSectionRect.pivot = new Vector2(0.5f, 0f); // bottom (almost) center
            charactersSectionObject.name = LobbyWindowController.CharactersSectionObjectName;
            charactersSectionObject.AddComponent<VerticalLayoutGroup>();
            var charactersSectionSizeFitter = charactersSectionObject.AddComponent<ContentSizeFitter>();
            charactersSectionSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var charactersSectionTitleObject = CreateDefaultGameObject(charactersSectionObject.transform);
            var charactersSectionTitleLayout = charactersSectionTitleObject.AddComponent<LayoutElement>();
            charactersSectionTitleLayout.preferredHeight = LobbySectionTitleHeight;
            charactersSectionTitleObject.name = LobbyWindowController.CharactersSectionTitleObjectName;
            var characterControlTitle = charactersSectionTitleObject.AddComponent<TextMeshProUGUI>();
            characterControlTitle.material = _defaultTextMesh.Material;
            characterControlTitle.color = _defaultTextMesh.Color;
            characterControlTitle.horizontalAlignment = HorizontalAlignmentOptions.Center;
            characterControlTitle.SetText(UIUtility.GetSaberBookFormat(UIStringConsts.LobbyInfoWindow.CharactersSectionTitle));
            var charactersSectionContentObject = CreateDefaultGameObject(charactersSectionObject.transform);
            charactersSectionContentObject.name = LobbyWindowController.CharactersSectionContentObjectName;
            charactersSectionContentObject.AddComponent<HorizontalLayoutGroup>();
            var preferedWidth = width / Main.MaxCharacters;
            for (int characterIndex = 0; characterIndex < Main.MaxCharacters; characterIndex++)
            {
                var characterObject = CreateDefaultGameObject(charactersSectionContentObject.transform);
                characterObject.name = LobbyWindowController.CharacterContainerObjectName;
                characterObject.AddComponent<VerticalLayoutGroup>();
                characterObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                var characterPortrait = CreateDefaultGameObject(characterObject.transform);
                characterPortrait.name = LobbyWindowController.CharacterPortraitObjectName;
                characterPortrait.AddComponent<Image>().color = Color.clear;
                var portraitLayoutElement = characterPortrait.AddComponent<LayoutElement>();
                portraitLayoutElement.preferredWidth = preferedWidth;
                portraitLayoutElement.preferredHeight = preferedWidth * 1.2f;

                var dropdownContainerObject = Main.Multiplayer.Factory.CreateDropdown(preferedWidth, characterObject.transform);
                dropdownContainerObject.name = LobbyWindowController.CharacterOwnerObjectName;
                var characterIndexComponent = dropdownContainerObject.AddComponent<CharacterIndexMonoBehaviour>();
                characterIndexComponent.CharacterIndex = characterIndex;

                var dropdownObject = dropdownContainerObject.transform.Find(UIFactory.DropdownGameObjectName);
                var tmpDropdown = dropdownObject.GetComponent<TMP_Dropdown>();
                tmpDropdown.interactable = interactableDropdown;
            }
        }
    }
}
