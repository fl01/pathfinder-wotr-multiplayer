using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using DG.Tweening;
using Kingmaker.Blueprints;
using Kingmaker.Localization;
using Kingmaker.UI.MVVM._VM.Tooltip.Templates;
using Kingmaker.UI.MVVM._VM.Tooltip.Utils;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.Controls.Button;
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

        public const string CharactersSectionObjectName = "CharactersSection";
        public const string CharactersSectionTitleObjectName = "CharactersSectionTitle";
        public const string CharactersSectionContentObjectName = "CharactersSectionContent";

        public const string CharacterContainerObjectName = "CharacterContainer";
        public const string CharacterPortraitObjectName = "CharacterPortrait";
        public const string CharacterOwnerObjectName = "CharacterOwner";

        private readonly ILogger<LobbyWindowController> _logger;
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

        public ILobbyWindow Window { get; private set; }

        private GameObject ServerInfoSectionContent => GetContentOwnedObject()?.transform
            .Find(LobbyContentObjectName)
            .Find(ServerInfoSectionObjectName)
            .Find(ServerInfoSectionContentObjectName).gameObject;

        private GameObject PlayersSectionContent => GetContentOwnedObject()?.transform
            .Find(LobbyContentObjectName)
            .Find(PlayersSectionObjectName)
            .Find(PlayersSectionContentObjectName).gameObject;

        private GameObject CharactersInfoContainer => GetContentOwnedObject()?.transform
            .Find(LobbyContentObjectName)
            .Find(CharactersSectionObjectName)
            .Find(CharactersSectionContentObjectName).gameObject;

        public LobbyWindowController(
            ILogger<LobbyWindowController> logger,
            IMainThreadAccessor mainThreadAccessor,
            IResourceProvider resourceProvider,
            IMultiplayerSettingsService multiplayerSettingsService,
            IMultiplayerActorAccessor multiplayerActorAccessor,
            IUIFactory uiFactory,
            UnityModManagerSettings unityModManagerSettings)
        {
            _logger = logger;
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
            Window.GetIsHost = () => _multiplayerActorAccessor.Host.IsActive;

            if (_multiplayerActorAccessor.Host.IsActive)
            {
                OnCharacterOwnerChanged = _multiplayerActorAccessor.Host.ChangeCharacterOwner;
            }

            if (_multiplayerActorAccessor.Client.IsActive)
            {
                _multiplayerActorAccessor.Client.OnCharacterOwnerChanged = character => UpdateCharacterOwnerDropdown(character, silent: true);
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

        public void UpdatePlayers(List<NetworkPlayer> players)
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

                UpdateCharacterOwnerDropdown(players);
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
                    ListenForDropdownChange(tmpDropdown);
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
                for (int characterIndex = 0; characterIndex < Main.MaxCharactersInParty; characterIndex++)
                {
                    var character = characters.Count > characterIndex ? characters[characterIndex] : null;
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
            var defaultMesh = _uiFactory.DefaultTextMesh;
            var playerContainerObject = _uiFactory.CreateDefaultGameObject(PlayersSectionContent.transform);
            playerContainerObject.name = PlayerContainerObjectName;
            var horizontal = playerContainerObject.AddComponent<HorizontalLayoutGroup>();
            horizontal.spacing = 6f;
            var playerContainerSizeFitter = playerContainerObject.AddComponent<ContentSizeFitter>();
            playerContainerSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            playerContainerSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            const int PreferredHeight = 28;

            CreateProgressBar(player, playerContainerObject.transform, PreferredHeight, withBackground: false);

            CreateLabel(playerContainerObject.transform, PreferredHeight, defaultMesh, $"[{player.ContentState.GameVersion}]");

            var playerObject = _uiFactory.CreateDefaultGameObject(playerContainerObject.transform);
            var playerElement = playerObject.AddComponent<LayoutElement>();
            playerElement.preferredHeight = PreferredHeight;
            playerObject.name = PlayerNameObjectName;
            var playerNameBox = playerObject.AddComponent<TextMeshProUGUI>();
            playerNameBox.alignment = TextAlignmentOptions.Center;
            playerNameBox.material = defaultMesh.Material;
            playerNameBox.color = defaultMesh.Color;
            playerNameBox.SetText(player.Name);
            playerNameBox.fontStyle = player.IsReady ? FontStyles.Normal : FontStyles.Strikethrough;

            if (player.IsReady)
            {
                CreatePlayerIcon("UI_journal_iconok_new2", playerContainerObject.transform, PreferredHeight, null);
            }

            if (player.ContentState.DiscrepantMods.Any() || player.ContentState.DiscrepantDLCs.Any())
            {
                var isMultiplayerModDifferent = player.ContentState.DiscrepantMods.Any(x => string.Equals(x.Id, _unityModManagerSettings.ModId, StringComparison.OrdinalIgnoreCase));
                var icon = isMultiplayerModDifferent ? "UI_QuestNotification_StampRed" : "UI_QuestNotification_StampYellow";
                CreatePlayerIcon(icon, playerContainerObject.transform, PreferredHeight, new ContentDiscrepancyTooltipTemplate(player));
            }
        }

        private void CreateLabel(Transform parent, int preferredHeight, Mesh mesh, string text)
        {
            var labelObject = _uiFactory.CreateDefaultGameObject(parent);
            var labelLayoutElement = labelObject.AddComponent<LayoutElement>();
            labelLayoutElement.preferredHeight = preferredHeight;
            var textBox = labelObject.AddComponent<TextMeshProUGUI>();
            textBox.alignment = TextAlignmentOptions.Center;
            textBox.material = mesh.Material;
            textBox.color = mesh.Color;
            textBox.SetText(text);
        }

        private void CreateProgressBar(NetworkPlayer networkPlayer, Transform parent, int size, bool withBackground = false)
        {
            var progressBar = _uiFactory.CreateProgressBar(parent, size, 0.45f, withBackground);
            progressBar.AddComponent<PlayerHandle>().Owner = networkPlayer;
        }

        private void CreatePlayerIcon(string iconName, Transform parent, int size, TooltipBaseTemplate template = null)
        {
            CreateIcon(parent, WellKnownResourceBundles.UI, iconName, size, template);
        }

        private void CreateIcon(Transform parent, string bundle, string iconName, int size, TooltipBaseTemplate template = null)
        {
            var iconObject = _uiFactory.CreateDefaultGameObject(parent);
            var layoutElement = iconObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = size;
            layoutElement.preferredWidth = size;
            var image = iconObject.AddComponent<Image>();
            var sprite = _resourceProvider.GetSprite(bundle, iconName);
            image.sprite = sprite;
            if (template != null)
            {
                var tooltipHandler = TooltipHelper.SetTooltip(image, template);
                _disposables.Add(tooltipHandler);
            }
        }

        private void DisposeDisposables()
        {
            foreach (var disposable in _disposables)
            {
                disposable.Dispose();
            }

            _disposables.Clear();

        }

        private void UpdateCharacterOwnerDropdown(List<NetworkPlayer> networkPlayers)
        {
            var options = networkPlayers.Select(x => new PlayerDropdownOptionData(x)).ToList<TMP_Dropdown.OptionData>();
            for (int characterIndex = 0; characterIndex < Main.MaxCharactersInParty; characterIndex++)
            {
                var characterContainer = CharactersInfoContainer.transform.GetChild(characterIndex);
                if (characterContainer == null)
                {
                    _logger.LogInformation("Unable to update character owner dropdown due to missing character container. Index={Index}", characterIndex);
                    return;
                }

                var dropdown = characterContainer.Find(CharacterOwnerObjectName);
                var dropdownObject = dropdown.transform.Find(UIFactory.DropdownGameObjectName);
                var tmpDropdown = dropdownObject.GetComponent<TMP_Dropdown>();
                RemoveAllDropdownListeners(tmpDropdown);
                tmpDropdown.onValueChanged.RemoveAllListeners();
                var selectedValue = tmpDropdown.value;
                tmpDropdown.ClearOptions();
                tmpDropdown.AddOptions(options);
                tmpDropdown.value = selectedValue;
                tmpDropdown.RefreshShownValue();
                ListenForDropdownChange(tmpDropdown);
            }
        }

        private void RemoveAllDropdownListeners(TMP_Dropdown dropdown)
        {
            dropdown.onValueChanged.RemoveAllListeners();
        }

        private void ListenForDropdownChange(TMP_Dropdown dropdown)
        {
            dropdown.onValueChanged.AddListener(index => OnOwnerDropdownChanged(dropdown));
        }

        private void UpdateCharacter(int characterIndex, NetworkCharacter character, Sprite portraitSprite, bool isDropdownInteractable)
        {
            var characterContainer = CharactersInfoContainer.transform.GetChild(characterIndex);
            if (characterContainer == null)
            {
                _logger.LogError("Character doesn't exist. Index={Index}", characterIndex);
                return;
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

        private void OnOwnerDropdownChanged(TMP_Dropdown dropdown)
        {
            var selectedOption = dropdown.options.Count >= dropdown.value ? dropdown.options[dropdown.value] : null;
            if (selectedOption == null || selectedOption is not PlayerDropdownOptionData playerOption)
            {
                _logger.LogWarning("Can't find selected dropdown option");
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
