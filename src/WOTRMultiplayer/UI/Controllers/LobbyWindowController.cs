using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AutoMapper;
using DG.Tweening;
using Kingmaker.Blueprints;
using Kingmaker.Localization;
using Kingmaker.UI.MVVM._VM.Tooltip.Templates;
using Kingmaker.UI.MVVM._VM.Tooltip.Utils;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.Controls.Button;
using Owlcat.Runtime.UI.Controls.Other;
using Owlcat.Runtime.UI.Tooltips;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using WOTRMultiplayer.Abstractions;
using WOTRMultiplayer.Abstractions.Settings;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Windows;
using WOTRMultiplayer.Abstractions.Unity;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.Connectivity;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.Services.Settings;
using WOTRMultiplayer.UI.Tooltips;
using WOTRMultiplayer.UI.Windows;
using WOTRMultiplayer.UnityBehaviours;

namespace WOTRMultiplayer.UI.Controllers
{
    public class LobbyWindowController : ILobbyWindowController
    {
        public const string PlaceholderPortrait = "Mask_Portrait";

        public const string LobbyScreenRootObjectName = "LobbyScreen";
        public const string LobbyContentObjectName = "LobbyContent";

        public const string ServerInfoSectionObjectName = "ServerInfoSection";
        public const string ServerInfoSectionTitleObjectName = "ServerInfoSectionTitle";
        public const string ServerInfoSectionContentObjectName = "ServerInfoSectionContent";

        public const string PlayersSectionObjectName = "PlayersSection";
        public const string PlayersSectionTitleObjectName = "PlayersSectionTitle";
        public const string PlayersSectionContentObjectName = "PlayersSectionContent";

        public const string PlayerContainerObjectName = "PlayerContainer";
        public const string PlayerNameObjectName = "PlayerName";
        public const string PlayerStatusObjectName = "PlayerStatus";

        public const string AdvancedControlSectionObjectName = "AdvancedControlsSection";
        public const string AdvancedControlSectionItemsObjectName = "AdvancedControlSectionItems";
        public const string AdvancedControlItemPlayerDropdownObjectName = "AdvancedControlItemPlayerDropdown";

        public const string CharactersSectionObjectName = "CharactersSection";
        public const string CharactersSectionTitleObjectName = "CharactersSectionTitle";
        public const string CharactersSectionContentObjectName = "CharactersSectionContent";
        public const string CharactersContentObjectName = "CharactersContent";

        public const string CharacterContainerObjectName = "CharacterContainer";
        public const string CharacterPortraitObjectName = "CharacterPortrait";
        public const string CharacterOwnerObjectName = "CharacterOwner";

        private readonly ILogger<LobbyWindowController> _logger;
        private readonly IMapper _mapper;
        private readonly IUIFactory _uiFactory;
        private readonly IMainThreadAccessor _mainThreadAccessor;
        private readonly IResourceProvider _resourceProvider;
        private readonly IMultiplayerSettingsService _multiplayerSettingsService;
        private readonly IMultiplayerActorAccessor _multiplayerActorAccessor;
        private readonly UnityModManagerSettings _unityModManagerSettings;

        private readonly ConcurrentDictionary<LobbyWindowOwner, GameObject> _contents = new();
        private LobbyWindowOwner _activeOwner;

        private readonly List<IDisposable> _disposables = [];

        public Action<NetworkCharacter, NetworkPlayer> OnCharacterOwnerChanged { get; set; }

        public Action<NetworkPlayerControlledFeature, NetworkPlayer> OnFeatureControlChanged { get; set; }

        public ILobbyWindow Window { get; private set; }

        private GameObject ServerInfoSectionContent => GetContentOwnedObject()?.transform
            .Find(LobbyContentObjectName)
            .Find(ServerInfoSectionObjectName)
            .Find(ServerInfoSectionContentObjectName).gameObject;

        private GameObject PlayersSectionContent => GetContentOwnedObject()?.transform
            .Find(LobbyContentObjectName)
            .Find(PlayersSectionObjectName)
            .Find(PlayersSectionContentObjectName).gameObject;

        private GameObject AdvancedControlsSectionItems => GetContentOwnedObject()?.transform
            .Find(LobbyContentObjectName)
            .Find(AdvancedControlSectionObjectName)
            .Find(AdvancedControlSectionItemsObjectName).gameObject;

        private GameObject CharactersInfoContainer => GetContentOwnedObject()?.transform
            .Find(LobbyContentObjectName)
            .Find(CharactersSectionObjectName)
            .Find(CharactersSectionContentObjectName)
            .Find(CharactersContentObjectName).gameObject;

        public LobbyWindowController(
            ILogger<LobbyWindowController> logger,
            IMapper mapper,
            IMainThreadAccessor mainThreadAccessor,
            IResourceProvider resourceProvider,
            IMultiplayerSettingsService multiplayerSettingsService,
            IMultiplayerActorAccessor multiplayerActorAccessor,
            IUIFactory uiFactory,
            UnityModManagerSettings unityModManagerSettings)
        {
            _logger = logger;
            _mapper = mapper;
            _uiFactory = uiFactory;
            _mainThreadAccessor = mainThreadAccessor;
            _resourceProvider = resourceProvider;
            _multiplayerSettingsService = multiplayerSettingsService;
            _multiplayerActorAccessor = multiplayerActorAccessor;
            _unityModManagerSettings = unityModManagerSettings;
        }

        public void CloseWindow()
        {
            if (Window != null && Window.IsVisible)
            {
                Window.Close();
            }
        }

        public void Reset()
        {
            Window = null;
            ResetOwnerContent(LobbyWindowOwner.EscMenu);
            OnCharacterOwnerChanged = null;
            OnFeatureControlChanged = null;
        }

        public void EnsureStandaloneWindowInitialized()
        {
            if (Window != null)
            {
                return;
            }

            Window = _uiFactory.InitializeEscMenuLobbyWindow(this);

            Window.GetGameConnectivity = _multiplayerActorAccessor.Current.GetGameConnectivity;
            Window.GetPlayers = _multiplayerActorAccessor.Current.GetPlayers;
            Window.GetCharacters = _multiplayerActorAccessor.Current.GetCharacters;
            Window.GetFeaturesControl = _multiplayerActorAccessor.Current.GetFeaturesControl;
            Window.GetIsHost = () => _multiplayerActorAccessor.Host.IsActive;

            if (_multiplayerActorAccessor.Host.IsActive)
            {
                OnCharacterOwnerChanged = _multiplayerActorAccessor.Host.ChangeCharacterOwner;
                OnFeatureControlChanged = _multiplayerActorAccessor.Host.ChangeFeatureControl;
            }

            if (_multiplayerActorAccessor.Client.IsActive)
            {
                _multiplayerActorAccessor.Client.OnCharacterOwnerChanged = character => UpdateCharacterOwnerDropdown(character, silent: true);
                _multiplayerActorAccessor.Client.OnFeaturesControlChanged = features => UpdateAdvancedControls(features, silent: true);
            }
        }

        public void InitializeContent(LobbyWindowOwner owner, Transform parent)
        {
            _logger.LogInformation("Initialize content. Owner={Owner}", owner);

            if (_contents.TryGetValue(owner, out var content) && content != null)
            {
                _logger.LogWarning("Lobby content still exists on the scene, skipping initialization. Owner={Owner}", owner);
                return;
            }

            var lobbyContent = _uiFactory.CreateLobbyWindowContent(parent);
            lobbyContent.SetActive(false);
            _contents.TryAdd(owner, lobbyContent);
            _logger.LogInformation("Content has been created. Owner={Owner}", owner);
        }

        public void UpdatePlayers(List<NetworkPlayer> players, bool isDropdownInteractable)
        {
            if (GetContentOwnedObject() == null)
            {
                return;
            }

            _mainThreadAccessor.Post(() =>
            {
                _logger.LogInformation("Updating player list. PlayersCount={PlayersCount}", players.Count);
                DisposeDisposables();
                PlayersSectionContent.CleanupAllChildren();
                foreach (var player in players)
                {
                    CreatePlayerObject(player);
                }

                UpdateCharactersOwnership(players);

                UpdateAdvancedControls(players, isDropdownInteractable);
            });
        }

        public void UpdateServerInfo(GameConnectivity connectivity)
        {
            var owner = GetContentOwnedObject();
            if (owner == null)
            {
                return;
            }

            owner.SetActive(true);

            ServerInfoSectionContent.CleanupAllChildren();

            var serverInfoContainerObject = _uiFactory.CreateDefaultGameObject(ServerInfoSectionContent.transform);
            serverInfoContainerObject.name = PlayerContainerObjectName;
            serverInfoContainerObject.AddComponent<VerticalLayoutGroup>();
            var serverInfoContainerSizeFitter = serverInfoContainerObject.AddComponent<ContentSizeFitter>();
            serverInfoContainerSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            serverInfoContainerSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (connectivity.Endpoint != null)
            {
                var settings = _multiplayerSettingsService.GetSettings();
                var endpointText = settings.HideServerAddress ? "***.***.***.***:****" : connectivity.Endpoint.ToString();
                AddServerInfo(serverInfoContainerObject.transform, endpointText);
            }

            if (connectivity.External == null)
            {
                return;
            }

            var externalConnectivityInfo = _uiFactory.CreateDefaultGameObject(serverInfoContainerObject.transform);
            var externalConnectivityHorizontalLayout = externalConnectivityInfo.AddComponent<HorizontalLayoutGroup>();
            externalConnectivityHorizontalLayout.spacing = 10f;

            var gameCodeTitle = new LocalizedString { Key = WellKnownKeys.LobbyWindow.Server.External.GameCode.Title.Key };
            AddServerInfo(externalConnectivityInfo.transform, $"{gameCodeTitle} -");

            Color? color = null;
            bool addInProgressAnimation = false;
            LocalizedString statusText = null;

            switch (connectivity.External.Status)
            {
                case ExternalConnectivityStatus.Connecting:
                    statusText = new LocalizedString { Key = WellKnownKeys.LobbyWindow.Server.External.State.Connecting.Key };
                    addInProgressAnimation = true;
                    break;
                case ExternalConnectivityStatus.Connected when string.IsNullOrEmpty(connectivity.External.Code):
                    statusText = new LocalizedString { Key = WellKnownKeys.LobbyWindow.Server.External.State.GettingCode.Key };
                    addInProgressAnimation = true;
                    break;
                case ExternalConnectivityStatus.Error:
                    statusText = new LocalizedString { Key = WellKnownKeys.LobbyWindow.Server.External.Errors.Generic.Key };
                    color = Color.red;
                    break;
            }

            if (statusText != null)
            {
                AddServerInfo(externalConnectivityInfo.transform, statusText, color, addInProgressAnimation);
                return;
            }

            AddServerInfo(externalConnectivityInfo.transform, connectivity.External.Code);
            var defaultCopySprite = _resourceProvider.GetSprite(WellKnownResourceBundles.UI, "UI_HUDIconNoAi_Default");
            var hoverCopySprite = _resourceProvider.GetSprite(WellKnownResourceBundles.UI, "UI_HUDIconNoAi_Hover");
            var pressedCopySprite = _resourceProvider.GetSprite(WellKnownResourceBundles.UI, "UI_HUDIconAi_Hover");
            var copyButtonObject = _uiFactory.CreateIconButton(externalConnectivityInfo.transform, defaultCopySprite, hoverCopySprite, pressedCopySprite);
            var copyButton = copyButtonObject.GetComponent<OwlcatButton>();
            copyButton.OnLeftClick.AddListener(() => GUIUtility.systemCopyBuffer = connectivity.External.Code);
            copyButton.OnRightClick.AddListener(() => GUIUtility.systemCopyBuffer = connectivity.External.Code);
            var copyButtonRect = copyButtonObject.GetComponent<RectTransform>();

            // TODO: seems like references are corrupted and usual TooltipHelper.SetTooltip can't trigger OnHover enter/exit events
            var tooltipText = new LocalizedString { Key = WellKnownKeys.LobbyWindow.Tooltips.CopyCode.Title.Key };
            var template = new TooltipTemplateSimple(tooltipText) { ContentSpacing = 0f };
            // sadly, there is no auto-width in place. Calculating it manually based on the font/length is an option, but lazy approach with 6x of the button size should be enough, right?
            var config = new TooltipConfig { Width = (int)copyButtonRect.sizeDelta.x * 6, PreferredHeight = 60 };
            copyButton.OnHover.AddListener(x => ShowTooltip(copyButton, x, template, config));
        }

        public void UpdateCharacterOwnerDropdown(NetworkCharacter character, bool silent = false)
        {
            _mainThreadAccessor.Post(() =>
            {
                var characterContainer = FindCharacterContainer(character);
                if (characterContainer == null)
                {
                    _logger.LogWarning("Unable to update character owner dropdown due to missing character container. CharacterName={CharacterName}, CharacterId={CharacterId}", character.Name, character.UnitId);
                    return;
                }

                var dropdown = characterContainer.Find(CharacterOwnerObjectName);
                var dropdownObject = dropdown.transform.Find(UIFactory.DropdownGameObjectName);
                var tmpDropdown = dropdownObject.GetComponent<TMP_Dropdown>();
                if (silent)
                {
                    RemoveAllDropdownListeners(tmpDropdown);
                }

                var playerOption = tmpDropdown.options.FirstOrDefault(o => o is PlayerDropdownOptionData player && player.Player.Id == character.Owner.Id);
                var optionIndex = tmpDropdown.options.IndexOf(playerOption);
                tmpDropdown.value = optionIndex;
                tmpDropdown.RefreshShownValue();
                if (silent)
                {
                    ListenForOwnerDropdownChange(tmpDropdown);
                }
            });
        }

        public void ResetData()
        {
            _logger.LogInformation("Reset all content");
            var current = GetContentOwnedObject();
            var playerSection = PlayersSectionContent;
            var serverSection = ServerInfoSectionContent;
            _mainThreadAccessor.Post(() =>
            {
                DisposeDisposables();
                current?.SetActive(false);
                playerSection?.CleanupAllChildren();
                serverSection?.CleanupAllChildren();
                UpdateCharacters([], false);
            });
        }

        public void SetActiveOwner(LobbyWindowOwner owner)
        {
            if (_activeOwner != owner)
            {
                _logger.LogInformation("Changing current owner. PreviousOwner={PreviousOwner}, NewOwner={NewOwner}", _activeOwner, owner);
                _activeOwner = owner;
            }
        }

        public void ResetOwnerContent(LobbyWindowOwner owner)
        {
            _logger.LogInformation("Reset owner content objects. Owner={Owner}", owner);
            _contents.TryRemove(owner, out var _);
        }

        public void UpdateCharacters(List<NetworkCharacter> characters, bool isDropdownInteractable)
        {
            if (GetContentOwnedObject() == null)
            {
                return;
            }

            _mainThreadAccessor.Post(() =>
            {
                // the plan is to dynamically create character containers in case of any mods that increase party size
                // 6 containers are pre-created by default
                var maxCharacters = Math.Max(CharactersInfoContainer.transform.childCount, characters.Count);
                for (int characterIndex = 0; characterIndex < maxCharacters; characterIndex++)
                {
                    var character = characters.Count > characterIndex ? characters[characterIndex] : null;
                    if (character == null && characterIndex >= UIFactory.MaxDisplayedCharactersUntilScroll)
                    {
                        CharactersInfoContainer.transform.GetChild(characterIndex).gameObject.SetActive(false);
                        continue;
                    }

                    var sprite = GetPortraitSprite(character);
                    UpdateCharacter(characterIndex, character, sprite, isDropdownInteractable);
                    if (character != null && character.Owner != null)
                    {
                        UpdateCharacterOwnerDropdown(character, silent: true);
                    }
                }
            });
        }

        public void UpdateLoadingProgress(Dictionary<long, float> progress)
        {
            _mainThreadAccessor.Post(() =>
            {
                foreach (Transform playerContainer in PlayersSectionContent.transform)
                {
                    var progressBar = playerContainer.Find(UIFactory.ProgressBarObjectName);
                    if (progress == null)
                    {
                        progressBar.gameObject.SetActive(false);
                        continue;
                    }

                    var player = progressBar.GetComponent<PlayerHandle>()?.Owner;
                    if (player != null && progress.TryGetValue(player.Id, out var playerProgress))
                    {
                        var progressImage = progressBar.Find(UIFactory.ProgressBarImageObjectName)?.GetComponent<Image>();
                        if (progressImage != null)
                        {
                            progressImage.fillAmount = Mathf.Clamp01(playerProgress);
                        }
                    }
                }
            });
        }

        public void UpdateAdvancedControls(IDictionary<NetworkPlayerControlledFeature, long> features, bool silent = false)
        {
            _mainThreadAccessor.Post(() =>
            {
                foreach (var feature in features)
                {
                    var featureRow = AdvancedControlsSectionItems.transform.Find(feature.Key.ToString());
                    if (featureRow == null)
                    {
                        _logger.LogWarning("Unable to find UI row for specified feature. Feature={Feature}", feature.Key);
                        continue;
                    }

                    var dropdown = featureRow.Find(AdvancedControlItemPlayerDropdownObjectName).Find(UIFactory.DropdownGameObjectName).GetComponent<TMP_Dropdown>();
                    var playerOption = dropdown.options.FirstOrDefault(o => o is PlayerDropdownOptionData playerDropdownOption && playerDropdownOption.Player.Id == feature.Value);
                    if (playerOption == null)
                    {
                        _logger.LogWarning("Unable to find player for specified feature. Feature={Feature}, PlayerId={PlayerId}", feature.Key, feature.Value);
                        continue;
                    }
                    var playerOptionValue = dropdown.options.IndexOf(playerOption);
                    if (silent)
                    {
                        dropdown.SetValueWithoutNotify(playerOptionValue);
                    }
                    else
                    {
                        dropdown.value = playerOptionValue;
                    }

                    dropdown.RefreshShownValue();
                }

                _logger.LogInformation("Player controlled features have been updated");
            });
        }

        private void ShowTooltip(MonoBehaviour component, bool isVisible, TooltipBaseTemplate tooltipBaseTemplate, TooltipConfig tooltipConfig)
        {
            if (isVisible)
            {
                TooltipHelper.ShowTooltip(component, tooltipBaseTemplate, tooltipConfig);
            }
            else
            {
                TooltipHelper.HideTooltip();
            }
        }

        private void AddServerInfo(Transform parent, string text, Color? color = null, bool addInProgressAnimation = false)
        {
            var serverInfoObject = _uiFactory.CreateDefaultGameObject(parent.transform);
            var serverInfoElement = serverInfoObject.AddComponent<LayoutElement>();
            serverInfoElement.preferredHeight = 40;
            var serverInfoBox = serverInfoObject.AddComponent<TextMeshProUGUI>();
            serverInfoBox.alignment = TextAlignmentOptions.Center;
            serverInfoBox.verticalAlignment = VerticalAlignmentOptions.Middle;
            serverInfoBox.horizontalAlignment = HorizontalAlignmentOptions.Center;
            serverInfoBox.material = _uiFactory.DefaultTextMesh.Material;
            serverInfoBox.color = color ?? _uiFactory.DefaultTextMesh.Color;
            // not sure why, but it's not actually centered for some reason
            serverInfoBox.margin = new Vector4(0f, 5f, 0f, 0f);
            if (addInProgressAnimation)
            {
                serverInfoBox.SetText(string.Empty);
                serverInfoBox.fontStyle = FontStyles.Italic;
                serverInfoBox.DOText(text, 2f)
                    .SetEase(Ease.Linear)
                    .SetLoops(-1, LoopType.Restart);
                return;
            }

            serverInfoBox.SetText(text);
        }

        private Transform FindCharacterContainer(NetworkCharacter character)
        {
            foreach (Transform child in CharactersInfoContainer.transform)
            {
                var dropdownCharacter = child.Find(CharacterOwnerObjectName)?.GetComponent<CharacterDataBehaviour>()?.Character;
                if (dropdownCharacter != null && (!string.IsNullOrEmpty(character.UnitId) && string.Equals(dropdownCharacter.UnitId, character.UnitId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(dropdownCharacter.Name, character.Name, StringComparison.OrdinalIgnoreCase) && dropdownCharacter.Index == character.Index))
                {
                    return child;
                }
            }

            return null;
        }

        private void CreatePlayerObject(NetworkPlayer player)
        {
            var playerContainerObject = _uiFactory.CreateDefaultGameObject(PlayersSectionContent.transform);
            playerContainerObject.name = PlayerContainerObjectName;
            var horizontal = playerContainerObject.AddComponent<HorizontalLayoutGroup>();
            horizontal.spacing = 6f;
            horizontal.childAlignment = TextAnchor.MiddleCenter;
            horizontal.childForceExpandHeight = false;
            var playerContainerSizeFitter = playerContainerObject.AddComponent<ContentSizeFitter>();
            playerContainerSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            playerContainerSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateProgressBar(player, playerContainerObject.transform, UIFactory.LobbyPlayerObjectHeight, withBackground: false);

            CreateLabel(playerContainerObject.transform, UIFactory.LobbyPlayerObjectHeight, $"[{player.ContentState.GameVersion}]");

            CreatePlayerColorIcon(playerContainerObject.transform, player);

            var playerNameObject = _uiFactory.CreateDefaultGameObject(playerContainerObject.transform);
            var playerNameElement = playerNameObject.AddComponent<LayoutElement>();
            playerNameElement.preferredHeight = UIFactory.LobbyPlayerObjectHeight;
            playerNameObject.name = PlayerNameObjectName;
            var playerNameBox = playerNameObject.AddComponent<TextMeshProUGUI>();
            playerNameBox.alignment = TextAlignmentOptions.Center;
            playerNameBox.material = _uiFactory.DefaultTextMesh.Material;
            playerNameBox.color = _uiFactory.DefaultTextMesh.Color;
            playerNameBox.SetText(player.Name);
            playerNameBox.fontStyle = player.IsReady ? FontStyles.Normal : FontStyles.Strikethrough;

            if (player.IsReady)
            {
                CreatePlayerIcon("UI_journal_iconok_new2", playerContainerObject.transform, UIFactory.LobbyPlayerObjectHeight, null);
            }

            if (player.ContentState.DiscrepantMods.Any() || player.ContentState.DiscrepantDLCs.Any())
            {
                var isMultiplayerModDifferent = player.ContentState.DiscrepantMods.Any(x => string.Equals(x.Id, _unityModManagerSettings.ModId, StringComparison.OrdinalIgnoreCase));
                var icon = isMultiplayerModDifferent ? "UI_QuestNotification_StampRed" : "UI_QuestNotification_StampYellow";
                CreatePlayerIcon(icon, playerContainerObject.transform, UIFactory.LobbyPlayerObjectHeight, new ContentDiscrepancyTooltipTemplate(player));
            }
        }

        private void CreatePlayerColorIcon(Transform parent, NetworkPlayer player)
        {
            var color = player.Color.ToUnityColor();
            var iconColor = _uiFactory.MuteColor(color);
            var playerIconObject = _uiFactory.CreateCircleIcon(parent, iconColor, 14f);
            var button = playerIconObject.AddComponent<OwlcatButton>();
            _disposables.Add(button.OnLeftClickAsObservable().Subscribe(_ => ShowColorPicker(player)));
            _disposables.Add(button.OnRightClickAsObservable().Subscribe(_ => ShowColorPicker(player)));
            var config = new TooltipConfig
            {
                InfoCallPCMethod = InfoCallPCMethod.None
            };

            var template = new TooltipTemplateSimple(
                new LocalizedString { Key = WellKnownKeys.LobbyWindow.Tooltips.Players.Color.Header.Key },
                new LocalizedString { Key = WellKnownKeys.LobbyWindow.Tooltips.Players.Color.Description.Key });
            var tooltip = TooltipHelper.SetTooltip(playerIconObject.GetComponent<Image>(), template, config);
            _disposables.Add(tooltip);
        }

        private void ShowColorPicker(NetworkPlayer networkPlayer)
        {
            _logger.LogWarning("Color picker. PlayerName={PlayerName}", networkPlayer.Name);
        }

        private void CreateLabel(Transform parent, int preferredHeight, string text)
        {
            var labelObject = _uiFactory.CreateDefaultGameObject(parent);
            var labelLayoutElement = labelObject.AddComponent<LayoutElement>();
            labelLayoutElement.preferredHeight = preferredHeight;
            var textBox = labelObject.AddComponent<TextMeshProUGUI>();
            textBox.alignment = TextAlignmentOptions.Center;
            textBox.material = _uiFactory.DefaultTextMesh.Material;
            textBox.color = _uiFactory.DefaultTextMesh.Color;
            textBox.SetText(text);
        }

        private void CreateProgressBar(NetworkPlayer networkPlayer, Transform parent, int size, bool withBackground = false)
        {
            var progressBar = _uiFactory.CreateProgressBar(parent, size, 0.45f, withBackground);
            progressBar.AddComponent<PlayerHandle>().Owner = networkPlayer;
        }

        private void CreatePlayerIcon(string iconName, Transform parent, int size, TooltipBaseTemplate template = null)
        {
            var tooltip = _uiFactory.CreateIcon(parent, WellKnownResourceBundles.UI, iconName, size, template);
            _disposables.Add(tooltip);
        }

        private void DisposeDisposables()
        {
            foreach (var disposable in _disposables)
            {
                disposable.Dispose();
            }

            _disposables.Clear();
        }

        private void UpdateAdvancedControls(List<NetworkPlayer> networkPlayers, bool isDropdownInteractable)
        {
            UpdatePlayerDropdowns(
                networkPlayers,
                AdvancedControlsSectionItems.transform,
                container => container.Find(AdvancedControlItemPlayerDropdownObjectName),
                dropdown => dropdown.onValueChanged.AddListener(_ => OnAdvancedControlDropdownChanged(dropdown)),
                shouldBeDisabled: !isDropdownInteractable);
        }

        private void UpdateCharactersOwnership(List<NetworkPlayer> networkPlayers)
        {
            UpdatePlayerDropdowns(
                networkPlayers,
                CharactersInfoContainer.transform,
                container => container.Find(CharacterOwnerObjectName),
                ListenForOwnerDropdownChange,
                shouldBeDisabled: false);
        }

        private void UpdatePlayerDropdowns(List<NetworkPlayer> networkPlayers, Transform container, Func<Transform, Transform> dropdownResolver, Action<TMP_Dropdown> listener, bool shouldBeDisabled)
        {
            var options = networkPlayers
                .Select(player => new PlayerDropdownOptionData(player))
                .ToList<TMP_Dropdown.OptionData>();

            var indexes = networkPlayers
                .Select((player, index) => new { player.Id, index })
                .ToDictionary(x => x.Id, x => x.index);

            foreach (Transform item in container)
            {
                var dropdown = dropdownResolver(item).Find(UIFactory.DropdownGameObjectName).GetComponent<TMP_Dropdown>();
                var selectedPlayerId = GetSelectedPlayerId(item, dropdown);

                RemoveAllDropdownListeners(dropdown);

                dropdown.ClearOptions();
                dropdown.AddOptions(options);

                if (selectedPlayerId >= 0 && indexes.TryGetValue(selectedPlayerId, out var playerIndex))
                {
                    dropdown.SetValueWithoutNotify(playerIndex);
                    dropdown.RefreshShownValue();
                }

                if (shouldBeDisabled)
                {
                    dropdown.interactable = false;
                }

                listener(dropdown);
            }
        }

        private long GetSelectedPlayerId(Transform item, TMP_Dropdown dropdown)
        {
            if (!item.gameObject.activeSelf || dropdown.value < 0 || dropdown.value >= dropdown.options.Count)
            {
                return -1;
            }

            return dropdown.options[dropdown.value] is PlayerDropdownOptionData selectedOption
                ? selectedOption.Player.Id
                : -1;
        }

        private void RemoveAllDropdownListeners(TMP_Dropdown dropdown)
        {
            dropdown.onValueChanged.RemoveAllListeners();
        }

        private void ListenForOwnerDropdownChange(TMP_Dropdown dropdown)
        {
            dropdown.onValueChanged.AddListener(index => OnOwnerDropdownChanged(dropdown));
        }

        private void UpdateCharacter(int characterIndex, NetworkCharacter character, Sprite portraitSprite, bool isDropdownInteractable)
        {
            var characterContainer = CharactersInfoContainer.transform.GetChild(characterIndex);
            if (characterContainer == null)
            {
                characterContainer = _uiFactory.CreateAdditionalCharacterContainer(CharactersInfoContainer.transform).transform;
                _logger.LogInformation("Extra character container has been created. Index={Index}", characterIndex);
            }

            var portraitObject = characterContainer.Find(CharacterPortraitObjectName);
            var portraitImage = portraitObject.GetComponent<Image>();
            portraitImage.sprite = portraitSprite;
            portraitImage.color = portraitSprite == null ? Color.clear : Color.white;
            var characterOwner = characterContainer.Find(CharacterOwnerObjectName);
            characterOwner.Find(UIFactory.DropdownGameObjectName).GetComponent<TMP_Dropdown>().interactable = isDropdownInteractable && portraitSprite != null;
            characterOwner.GetComponent<CharacterDataBehaviour>().Character = character;
            _logger.LogInformation("Updated character portrait. Index={Index}, CharacterName={CharacterName}, CharacterId={CharacterId}, SpriteName={SpriteName}", characterIndex, character?.Name, character?.UnitId, portraitSprite?.name);
        }

        private void OnAdvancedControlDropdownChanged(TMP_Dropdown dropdown)
        {
            var selectedOption = dropdown.options.Count >= dropdown.value ? dropdown.options[dropdown.value] : null;
            if (selectedOption == null || selectedOption is not PlayerDropdownOptionData playerOption)
            {
                _logger.LogWarning("AdvancedControl dropdown contains an invalid option data");
                return;
            }

            var feature = dropdown.transform.parent.gameObject.GetComponent<PlayerControlledFeatureBehaviour>().Feature;
            _logger.LogInformation("AdvancedControl dropdown changed. Feature={Feature}, PlayerId={PlayerId}", feature, playerOption.Player.Id);
            OnFeatureControlChanged?.Invoke(feature, playerOption.Player);
        }

        private void OnOwnerDropdownChanged(TMP_Dropdown dropdown)
        {
            var selectedOption = dropdown.options.Count >= dropdown.value ? dropdown.options[dropdown.value] : null;
            if (selectedOption == null || selectedOption is not PlayerDropdownOptionData playerOption)
            {
                _logger.LogWarning("CharacterOwner dropdown contains an invalid option data");
                return;
            }

            var character = dropdown.transform.parent?.GetComponent<CharacterDataBehaviour>()?.Character;
            if (character == null)
            {
                _logger.LogError("Character info is missing for the changed dropdown");
                return;
            }

            _logger.LogInformation("Character owner changed. CharacterName={CharacterName}, CharacterId={CharacterId}, PlayerId={PlayerId}, PlayerName={PlayerName}", character.Name, character.UnitId, playerOption.Player.Id, playerOption.Player.Name);
            OnCharacterOwnerChanged?.Invoke(character, playerOption.Player);
        }

        private Sprite GetPortraitSprite(NetworkCharacter character)
        {
            if (character == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(character.CustomPortraitId))
            {
                var customPortrait = new PortraitData(character.CustomPortraitId);
                return customPortrait.SmallPortrait;
            }

            var portrait = _resourceProvider.GetSprite(WellKnownResourceBundles.Portraits, character.Portrait) ?? _resourceProvider.GetSprite(WellKnownResourceBundles.Portraits, PlaceholderPortrait);
            if (portrait == null)
            {
                _logger.LogWarning("Unable to load character portrait. PortraitName={PortraitName}", character.Portrait);
            }

            return portrait;
        }

        private GameObject GetContentOwnedObject([CallerMemberName] string callerName = "")
        {
            if (!_contents.TryGetValue(_activeOwner, out var content) || content == null)
            {
                _logger.LogWarning("[{CallerName}] Content doesn't exist for the current owner. Owner={Owner}", callerName, _activeOwner);
                return null;
            }

            return content;
        }

        private class PlayerDropdownOptionData : TMP_Dropdown.OptionData
        {
            public NetworkPlayer Player { get; private set; }

            public PlayerDropdownOptionData(NetworkPlayer player)
            {
                Player = player;

                base.text = player.Name;
            }
        }
    }
}
