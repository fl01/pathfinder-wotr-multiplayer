using System;
using System.Collections.Generic;
using FluentValidation;
using Kingmaker;
using Kingmaker.Localization;
using Kingmaker.Settings;
using Kingmaker.UI;
using Kingmaker.UI.Common;
using Kingmaker.UI.MVVM._PCView.Common;
using Kingmaker.UI.MVVM._PCView.ContextMenu;
using Kingmaker.UI.MVVM._PCView.EscMenu;
using Kingmaker.UI.MVVM._PCView.SaveLoad;
using Kingmaker.UI.MVVM._PCView.Settings.Entities;
using Kingmaker.UI.MVVM._VM.ContextMenu;
using Kingmaker.UI.MVVM._VM.Settings;
using Kingmaker.UI.MVVM._VM.Settings.Entities;
using Kingmaker.UI.MVVM._VM.Settings.Entities.Decorative;
using Kingmaker.UI.ServiceWindow.Credits;
using Kingmaker.UI.SettingsUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.Controls.Button;
using Owlcat.Runtime.UI.MVVM;
using Owlcat.Runtime.UI.VirtualListSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Controllers.Menu;
using WOTRMultiplayer.Abstractions.UI.Windows;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.HarmonyPatches.MenuPatches;
using WOTRMultiplayer.Services.Settings;
using WOTRMultiplayer.Services.Settings.Validators;
using WOTRMultiplayer.UI.Behaviours;
using WOTRMultiplayer.UI.Controllers;
using WOTRMultiplayer.UI.Menu;
using WOTRMultiplayer.UI.Settings;
using WOTRMultiplayer.UI.Settings.Entities;

namespace WOTRMultiplayer.UI
{
    public class UIFactory : IUIFactory
    {
        public const UISettingsManager.SettingsScreen MultiplayerSettingsMenuId = (UISettingsManager.SettingsScreen)125651235;

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

        private readonly ILogger<UIFactory> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IUIAccessor _uiAccessor;

        public UIFactory(
            ILogger<UIFactory> logger,
            IServiceProvider serviceProvider,
            IUIAccessor uiAccessor)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _uiAccessor = uiAccessor;
        }

        public void StoreDropdownPrefab(SettingsEntityDropdownPCView view)
        {
            if (view == null || _dropdownPrefab != null)
            {
                return;
            }

            _logger.LogInformation("Storing {PrefabTypeName} prefab", nameof(SettingsEntityDropdownPCView));

            _dropdownPrefab = UnityEngine.Object.Instantiate(view.gameObject);
            UnityEngine.Object.DestroyImmediate(_dropdownPrefab.GetComponent<SettingsEntityDropdownPCView>());
            UnityEngine.Object.DestroyImmediate(_dropdownPrefab.GetComponent<ContentSizeFitterExtended>());
            UnityEngine.Object.DestroyImmediate(_dropdownPrefab.GetComponent<VerticalLayoutGroupWorkaround>());
            UnityEngine.Object.DestroyImmediate(_dropdownPrefab.GetComponent<LayoutElement>());
            UnityEngine.Object.DontDestroyOnLoad(_dropdownPrefab);
        }

        public void StoreSaveLoadPCViewPrefab(SaveLoadPCView view)
        {
            if (view == null || _saveLoadPCView != null)
            {
                _logger.LogWarning("SaveLoadPCView is null");
                return;
            }

            _logger.LogInformation("Storing {PrefabTypeName} prefab", nameof(SaveLoadPCView));
            _saveLoadPCView = UnityEngine.Object.Instantiate(view);
            UnityEngine.Object.DontDestroyOnLoad(_saveLoadPCView);
        }

        public void StoreDefaultGameObject(GameObject gameObject)
        {
            if (gameObject == null || _defaultGameObject != null)
            {
                return;
            }

            _logger.LogInformation("Storing default prefab");
            _defaultGameObject = UnityEngine.Object.Instantiate(gameObject);
            UnityEngine.Object.DestroyImmediate(_defaultGameObject.GetComponent<Image>());
            UnityEngine.Object.DontDestroyOnLoad(_defaultGameObject);
        }

        public void StoreBorderDecoration(GameObject gameObject)
        {
            if (gameObject == null || _borderDecoration != null)
            {
                return;
            }

            _logger.LogInformation("Storing border decoration prefab");
            _borderDecoration = UnityEngine.Object.Instantiate(gameObject);
            UnityEngine.Object.DontDestroyOnLoad(_borderDecoration);
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

        public GameObject CreateDropdown(float preferedWidth, Transform parent)
        {
            var dropdownContainerObject = UnityEngine.Object.Instantiate(_dropdownPrefab, parent);
            dropdownContainerObject.CleanupAllChildren(x => x.name != DropdownGameObjectName);
            var dropdownObject = dropdownContainerObject.transform.Find(DropdownGameObjectName);
            var rect = dropdownObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(preferedWidth, rect.sizeDelta.y);

            var dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
            dropdown.ClearOptions();

            var templateRect = dropdownObject.Find("Template").GetComponent<RectTransform>();
            templateRect.sizeDelta = new Vector2(Math.Abs(templateRect.sizeDelta.x * 5), templateRect.sizeDelta.y);

            var labelRect = dropdownObject.Find("Label").GetComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(labelRect.sizeDelta.x * 0.5f, labelRect.sizeDelta.y);

            return dropdownContainerObject;
        }

        public IMultiplayerWindow InitializeMultiplayerWindow(InitializeMultiplayerContext context, Action onShow)
        {
            var multiplayerMenu = UnityEngine.Object.Instantiate(context.MenuItemPrototype, context.Parent);
            multiplayerMenu.transform.SetSiblingIndex(context.MenuItemPrototype.transform.GetSiblingIndex());
            var multiplayerMenuView = multiplayerMenu.GetComponent<ContextMenuEntityPCView>();
            var element = CreateCopyOfCreditsScreen();
            var multiplayerWindow = element.AddComponent<MultiplayerWindow>()
                .WithLogger(_serviceProvider.GetService<ILogger<MultiplayerWindow>>())
                .WithControllers(_serviceProvider.GetService<IHostMenuItemController>(), _serviceProvider.GetService<IJoinMenuItemController>());
            multiplayerWindow.Initialize();

            CreateBackgroundArt(multiplayerWindow.transform.Find("BackgroundGroup"));
            var text = UIUtility.GetSaberBookFormat(new LocalizedString { Key = WellKnownKeys.MainMenu.Multiplayer.Title.Key });
            var viewModel = new ContextMenuEntityVM(new ContextMenuCollectionEntity(UIUtility.GetSaberBookFormat(text), onShow));
            multiplayerMenuView.Bind(viewModel);

            // extra menu item = shift up %
            float shiftY = context.Parent.position.y * 1.5f;
            var newPosition = new Vector3(context.Parent.position.x, shiftY, context.Parent.position.z);
            context.Parent.SetPositionAndRotation(newPosition, context.Parent.rotation);

            return multiplayerWindow;
        }

        private GameObject CreateCopyOfCreditsScreen()
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

        public GameObject CreateLobbyWindowContent(Transform parent)
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
            CreateLobbyCharactersSection(width, verticalContent.transform);

            return lobbyContent;
        }

        public ILobbyWindow InitializeEscMenuLobbyWindow(ILobbyWindowController controller, Action onShow)
        {
            var escMenuView = _uiAccessor.EscMenu;

            _logger.LogInformation("Adding new menu 'Multiplayer Lobby' to EscMenu");
            var optionsButton = escMenuView.transform.Find("Window/ButtonBlock/OptionsButton");
            var multiplayerLobbyMenuItem = UnityEngine.Object.Instantiate(optionsButton.gameObject, optionsButton.transform.parent);
            multiplayerLobbyMenuItem.transform.SetSiblingIndex(optionsButton.GetSiblingIndex());
            multiplayerLobbyMenuItem.name = MultiplayerMenuObjectName;
            var textObject = multiplayerLobbyMenuItem.transform.Find("Text");
            UnityEngine.Object.DestroyImmediate(textObject.GetComponent<LocalizedUIText>());
            textObject.GetComponent<TextMeshProUGUI>().SetText(new LocalizedString { Key = WellKnownKeys.EscMenu.MultiplayerLobby.Title.Key });

            _logger.LogInformation("Initializing 'Multiplayer Lobby' Window");
            var windowContainer = UnityEngine.Object.Instantiate(escMenuView.gameObject, escMenuView.transform.parent);
            windowContainer.name = "MultiplayerLobbyWindow";
            // must be located before the tooltip game-objects to avoid obstruction
            var infoWindowViewIndex = windowContainer.transform.parent.Find("InfoWindowPCViewBig").GetSiblingIndex();
            var tooltipViewIndex = windowContainer.transform.parent.Find("TooltipView").GetSiblingIndex();
            var windowIndex = Math.Min(infoWindowViewIndex, tooltipViewIndex);
            windowContainer.transform.SetSiblingIndex(windowIndex);

            UnityEngine.Object.DestroyImmediate(windowContainer.GetComponent<EscMenuPCView>());
            windowContainer.transform.Find("Window").gameObject.CleanupAllChildren();

            var lobbyContainer = CreateDefaultGameObject(windowContainer.transform);
            var lobbyContainerRect = lobbyContainer.GetComponent<RectTransform>();
            var lobbyWidth = Math.Min(Screen.width * 0.45f, 1444);
            var lobbyHeight = Math.Min(Screen.height * 0.65f, 1000);
            lobbyContainerRect.sizeDelta = new Vector2(lobbyWidth, lobbyHeight);
            lobbyContainerRect.anchorMin = new Vector2(0.5f, 0.5f);
            lobbyContainerRect.anchorMax = new Vector2(0.5f, 0.5f);
            lobbyContainerRect.pivot = new Vector2(0.5f, 0.5f);

            var background = CreateBackgroundArt(lobbyContainer.transform);
            UnityEngine.Object.DestroyImmediate(background.transform.Find("Art").gameObject);
            var backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = lobbyContainerRect.anchorMin;
            backgroundRect.anchorMax = lobbyContainerRect.anchorMax;
            backgroundRect.sizeDelta = new Vector2(lobbyContainerRect.sizeDelta.x * 1.1f, lobbyContainerRect.sizeDelta.y * 1.1f);
            backgroundRect.pivot = lobbyContainerRect.pivot;

            var lobbyWindow = lobbyContainer.AddComponent<LobbyWindow>()
                .WithLogger(_serviceProvider.GetService<ILogger<LobbyWindow>>())
                .WithInitiator(multiplayerLobbyMenuItem)
                .WithController(controller)
                .WithCloseHandler(() => windowContainer.SetActive(false))
                .Initialize(LobbyWindowOwner.EscMenu);

            var multiplayerLobbyButton = multiplayerLobbyMenuItem.GetComponent<OwlcatButton>();
            multiplayerLobbyButton.OnLeftClick.RemoveAllListeners();
            multiplayerLobbyButton.OnLeftClick.AddListener(() =>
            {
                escMenuView.m_CloseButton.m_OnLeftClick.Invoke();
                windowContainer.SetActive(true);
                onShow();
            });


            windowContainer.SetActive(false);
            return lobbyWindow;
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
            if (inputObject == null || _inputPrefab != null)
            {
                return;
            }

            _logger.LogInformation("Storing input prefab");

            _inputPrefab = UnityEngine.Object.Instantiate(inputObject);
            var placeHolderTextObject = _inputPrefab.transform.Find(InputPlaceholderObjectName);
            var localizedTextComponent = placeHolderTextObject.GetComponent<LocalizedUIText>();
            if (localizedTextComponent != null)
            {
                UnityEngine.Object.DestroyImmediate(localizedTextComponent);
            }

            UnityEngine.Object.DontDestroyOnLoad(_inputPrefab);
        }

        public void StoreButtonPrefab(GameObject buttonObject)
        {
            if (buttonObject == null || _buttonPrefab != null)
            {
                return;
            }

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
            UnityEngine.Object.DontDestroyOnLoad(_buttonPrefab);
        }

        public GameObject CreateBackgroundArt(Transform parent)
        {
            return UnityEngine.Object.Instantiate(_backgroundArtPrefab, parent);
        }

        public void StoreBackgroundArt(GameObject backgroundArt)
        {
            if (backgroundArt == null || _backgroundArtPrefab != null)
            {
                return;
            }

            _logger.LogInformation("Storing background art");

            _backgroundArtPrefab = UnityEngine.Object.Instantiate(backgroundArt);
            UnityEngine.Object.DontDestroyOnLoad(_backgroundArtPrefab);
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
            serverInfoTitle.SetText(UIUtility.GetSaberBookFormat(new LocalizedString { Key = WellKnownKeys.LobbyWindow.Server.Title.Key }));

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
            playersTitle.SetText(UIUtility.GetSaberBookFormat(new LocalizedString { Key = WellKnownKeys.LobbyWindow.Players.Title.Key }));

            var playersSectionContentObject = CreateDefaultGameObject(playersSectionObject.transform);
            playersSectionContentObject.name = LobbyWindowController.PlayersSectionContentObjectName;
            playersSectionContentObject.AddComponent<VerticalLayoutGroup>();
        }

        private void CreateLobbyCharactersSection(float width, Transform parent)
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
            characterControlTitle.SetText(UIUtility.GetSaberBookFormat(new LocalizedString { Key = WellKnownKeys.LobbyWindow.Characters.Title.Key }));
            var charactersSectionContentObject = CreateDefaultGameObject(charactersSectionObject.transform);
            charactersSectionContentObject.name = LobbyWindowController.CharactersSectionContentObjectName;
            charactersSectionContentObject.AddComponent<HorizontalLayoutGroup>();
            var preferedWidth = width / Main.MaxCharactersInParty;
            for (int characterIndex = 0; characterIndex < Main.MaxCharactersInParty; characterIndex++)
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
            }
        }

        public void DestroyLobbyWindow(ILobbyWindow lobbyWindow)
        {
            if (lobbyWindow == null)
            {
                _logger.LogWarning("Lobby window is null");
                return;
            }

            lobbyWindow.GetGameConnectivity = null;
            lobbyWindow.GetPlayers = null;
            lobbyWindow.GetCharacters = null;
            lobbyWindow.GetIsHost = null;

            if (lobbyWindow.Initiator == null)
            {
                _logger.LogWarning("Lobby Initiator is null");
                return;
            }

            UnityEngine.Object.DestroyImmediate(lobbyWindow.Initiator);
        }

        public void PopulateMultiplayerSettingsUI(SettingsVM settingsVM)
        {
            settingsVM.m_SettingEntities.Clear();

            foreach (var settingEntity in GetSettingEntities())
            {
                settingsVM.m_SettingEntities.Add(settingsVM.AddDisposableAndReturn(settingEntity));
            }

            _logger.LogInformation("Multiplayer settings have been populated");
        }

        public IVirtualListElementView InitializeInputSettingTemplate(GameObject settingPrefab)
        {
            var stringView = UnityEngine.Object.Instantiate(settingPrefab);
            UnityEngine.Object.DestroyImmediate(stringView.GetComponent<SettingsEntityBoolPCView>());
            UnityEngine.Object.DontDestroyOnLoad(stringView);

            var view = stringView.AddComponent<SettingsEntityInputView>();
            var inputContainer = stringView.transform.Find("MultiButton").gameObject;
            var placeAt = inputContainer.transform.Find("OffText");
            var placeAtRect = placeAt.GetComponent<RectTransform>();
            var input = Main.Multiplayer.Factory.CreateInput(inputContainer.transform);
            input.transform.SetPositionAndRotation(placeAt.position, placeAt.rotation);
            var inputRect = input.GetComponent<RectTransform>();
            inputRect.pivot = placeAtRect.pivot;
            inputRect.anchorMin = placeAtRect.anchorMin;
            inputRect.anchorMax = placeAtRect.anchorMax;
            inputRect.offsetMin = placeAtRect.offsetMin;
            inputRect.offsetMax = placeAtRect.offsetMax;
            inputRect.sizeDelta = new Vector2(inputRect.sizeDelta.x * 2.75f, inputRect.sizeDelta.y);
            inputContainer.CleanupAllChildren(x => x.name != input.name);

            return view;
        }

        private IEnumerable<VirtualListElementVMBase> GetSettingEntities()
        {
            yield return new SettingsEntityHeaderVM(new LocalizedString { Key = WellKnownKeys.Settings.General.Title.Key });
            yield return CreateStringInputSetting(
                WellKnownKeys.Settings.General.PlayerName.Title.Key,
                WellKnownKeys.Settings.General.PlayerName.Tooltip.Key,
                WellKnownSettings.General.PlayerName,
                new PlayerNameValidator(),
                PlayerNameValidator.MaxLength);

            yield return new SettingsEntityHeaderVM(new LocalizedString { Key = WellKnownKeys.Settings.Combat.Title.Key });
            yield return CreateBoolSetting(WellKnownKeys.Settings.Combat.SyncAI.Title.Key, WellKnownKeys.Settings.Combat.SyncAI.Tooltip.Key, WellKnownSettings.Combat.AISync);

            yield return new SettingsEntityHeaderVM(new LocalizedString { Key = WellKnownKeys.Settings.Networking.Title.Key });
            yield return CreateIntInputSetting(
                WellKnownKeys.Settings.Networking.HostPortRangeStart.Title.Key,
                WellKnownKeys.Settings.Networking.HostPortRangeStart.Tooltip.Key,
                WellKnownSettings.Networking.HostPortRangeStart,
                new NetworkPortValidator(),
                NetworkPortValidator.MaxCharacters);
            yield return CreateIntInputSetting(
                WellKnownKeys.Settings.Networking.HostPortRangeEnd.Title.Key,
                WellKnownKeys.Settings.Networking.HostPortRangeEnd.Tooltip.Key,
                WellKnownSettings.Networking.HostPortRangeEnd,
                new NetworkPortValidator(),
                NetworkPortValidator.MaxCharacters);

            yield return new SettingsEntityHeaderVM(new LocalizedString { Key = WellKnownKeys.Settings.Miscellaneous.Title.Key });
            yield return CreateBoolSetting(WellKnownKeys.Settings.Miscellaneous.HideServerAddress.Title.Key, WellKnownKeys.Settings.Miscellaneous.HideServerAddress.Tooltip.Key, WellKnownSettings.Miscellaneous.HideServerAddress);

            yield return new SettingsEntityHeaderVM(new LocalizedString { Key = WellKnownKeys.Settings.DangerZone.Title.Key });
            yield return CreateStringInputSetting(
                WellKnownKeys.Settings.DangerZone.DefaultForcedPauseTimeout.Title.Key,
                WellKnownKeys.Settings.DangerZone.DefaultForcedPauseTimeout.Tooltip.Key,
                WellKnownSettings.DangerZone.DefaultForcedPauseTimeout,
                new TimeSpanValidator(),
                TimeSpanValidator.MaxLength);
            yield return CreateStringInputSetting(
                WellKnownKeys.Settings.DangerZone.RestEncounterForcedPauseTimeout.Title.Key,
                WellKnownKeys.Settings.DangerZone.RestEncounterForcedPauseTimeout.Tooltip.Key,
                WellKnownSettings.DangerZone.RestEncounterForcedPauseTimeout,
                new TimeSpanValidator(),
                TimeSpanValidator.MaxLength);
            yield return CreateStringInputSetting(
                WellKnownKeys.Settings.DangerZone.RestEncounterSyncTimeout.Title.Key,
                WellKnownKeys.Settings.DangerZone.RestEncounterSyncTimeout.Tooltip.Key,
                WellKnownSettings.DangerZone.RestEncounterSyncTimeout,
                new TimeSpanValidator(),
                TimeSpanValidator.MaxLength);
            yield return CreateStringInputSetting(
                WellKnownKeys.Settings.DangerZone.RemoteRollRetrievalTimeout.Title.Key,
                WellKnownKeys.Settings.DangerZone.RemoteRollRetrievalTimeout.Tooltip.Key,
                WellKnownSettings.DangerZone.RemoteRollRetrievalTimeout,
                new TimeSpanValidator(),
                TimeSpanValidator.MaxLength);
            yield return CreateStringInputSetting(
                WellKnownKeys.Settings.DangerZone.NetworkAwaiterTimeout.Title.Key,
                WellKnownKeys.Settings.DangerZone.NetworkAwaiterTimeout.Tooltip.Key,
                WellKnownSettings.DangerZone.NetworkAwaiterTimeout,
                new TimeSpanValidator(),
                TimeSpanValidator.MaxLength);
            yield return CreateStringInputSetting(
                WellKnownKeys.Settings.DangerZone.SyncAITimeout.Title.Key,
                WellKnownKeys.Settings.DangerZone.SyncAITimeout.Tooltip.Key,
                WellKnownSettings.DangerZone.AISyncTimeout,
                new TimeSpanValidator(),
                TimeSpanValidator.MaxLength);
        }

        private SettingsEntityBoolVM CreateBoolSetting(string titleKey, string tooltipKey, WellKnownSettingKey<bool> settingKey)
        {
            var boolSetting = ScriptableObject.CreateInstance<UISettingsEntityBool>();
            ConfigureSetting(boolSetting, titleKey, tooltipKey);
            var setting = new SettingsEntityBool(settingKey.Key, settingKey.DefaultValue);
            boolSetting.LinkSetting(setting);

            var viewModel = new SettingsEntityBoolVM(boolSetting);
            return viewModel;
        }

        private SettingsEntityStringInputVM CreateStringInputSetting(
            string titleKey,
            string tooltipKey,
            WellKnownSettingKey<string> settingKey,
            AbstractValidator<string> validator,
            int characterLimit)
        {
            var inputSetting = ScriptableObject.CreateInstance<UIValidatableStringSettingsEntity>();
            ConfigureSetting(inputSetting, titleKey, tooltipKey);
            ConfigureValidation(inputSetting, validator, characterLimit);
            var setting = new SettingsEntityString(settingKey.Key, settingKey.DefaultValue);
            inputSetting.LinkSetting(setting);

            var viewModel = new SettingsEntityStringInputVM(inputSetting);
            return viewModel;
        }

        private SettingsEntityIntInputVM CreateIntInputSetting(
            string titleKey,
            string tooltipKey,
            WellKnownSettingKey<int> settingKey,
            AbstractValidator<int> validator,
            int characterLimit)
        {
            var inputSetting = ScriptableObject.CreateInstance<UIValidatableIntSettingsEntity>();
            ConfigureSetting(inputSetting, titleKey, tooltipKey);
            ConfigureValidation(inputSetting, validator, characterLimit);
            var setting = new SettingsEntityInt(settingKey.Key, settingKey.DefaultValue);
            inputSetting.LinkSetting(setting);

            var viewModel = new SettingsEntityIntInputVM(inputSetting);
            return viewModel;
        }

        private void ConfigureValidation<TValue>(UIValidatableSettingsEntityBase<TValue> uiValidatableSettingsEntityBase, AbstractValidator<TValue> validator, int characterLimit)
        {
            uiValidatableSettingsEntityBase.Validator = validator;
            uiValidatableSettingsEntityBase.CharacterLimit = characterLimit;
        }

        private void ConfigureSetting<TValue>(UISettingsEntityWithValueBase<TValue> uiSettingsEntityBase, string titleKey, string tooltipKey)
        {
            uiSettingsEntityBase.m_Description = new LocalizedString { Key = titleKey };
            uiSettingsEntityBase.m_TooltipDescription = new LocalizedString { Key = tooltipKey };

            // should be replaced by _multiplayerAccessor.Current == null
            // but static call is fine as this entire class is untestable anyway due to direct dependency on Unity types
            uiSettingsEntityBase.ManualModificationLock = Main.Multiplayer.IsActive;
            uiSettingsEntityBase.ModificationAllowedCheck = () => true;
        }

        public void CreateMultiplayerSettingsMenu(SettingsVM settingsVM)
        {
            var title = new LocalizedString { Key = WellKnownKeys.Settings.Title.Key };
            settingsVM.CreateMenuEntity(title, MultiplayerSettingsMenuId);
            Main.GetLogger<SettingsVMPatches>().LogInformation("Multiplayer settings menu has been added");
        }
    }
}
