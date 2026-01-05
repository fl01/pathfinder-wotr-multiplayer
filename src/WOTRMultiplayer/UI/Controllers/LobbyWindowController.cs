using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Kingmaker.UI.MVVM._VM.Tooltip.Utils;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.Tooltips;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using WOTRMultiplayer.Abstractions.Settings;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.Unity;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.UI.Behaviours;
using WOTRMultiplayer.UI.Menu;
using WOTRMultiplayer.UI.Tooltips;

namespace WOTRMultiplayer.UI.Controllers
{
    public class LobbyWindowController : ILobbyWindowController
    {
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
        private readonly IUIFactory _uIFactory;
        private readonly IMainThreadAccessor _mainThreadAccessor;
        private readonly IResourceProvider _resourceProvider;
        private readonly IMultiplayerSettingsService _multiplayerSettingsService;
        private readonly ConcurrentDictionary<LobbyWindowOwner, GameObject> _contents = new();
        private LobbyWindowOwner _activeOwner;

        private readonly List<IDisposable> _disposables = [];

        public Action<int, int> OnCharacterOwnerChanged { get; set; }

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
            IUIFactory uIFactory)
        {
            _logger = logger;
            _uIFactory = uIFactory;
            _mainThreadAccessor = mainThreadAccessor;
            _resourceProvider = resourceProvider;
            _multiplayerSettingsService = multiplayerSettingsService;
        }

        public void InitializeContent(LobbyWindowOwner owner, Transform parent)
        {
            _logger.LogInformation("Initialize content. Owner={Owner}", owner);

            if (_contents.TryGetValue(owner, out var content) && content != null)
            {
                _logger.LogWarning("Lobby content still exists on the scene, skipping initialization. Owner={Owner}", owner);
                return;
            }

            var lobbyContent = _uIFactory.CreateLobbyWindowContent(parent);
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

        public void UpdateServerInfo(NetworkGameConnectivity connectivity)
        {
            if (GetContentOwnedObject() == null)
            {
                return;
            }

            GetContentOwnedObject().SetActive(true);

            ServerInfoSectionContent.CleanupAllChildren();

            var defaultMesh = Main.Multiplayer.Factory.GetDefaultMesh();
            var serverInfoContainerObject = Main.Multiplayer.Factory.CreateDefaultGameObject(ServerInfoSectionContent.transform);
            serverInfoContainerObject.name = PlayerContainerObjectName;
            serverInfoContainerObject.AddComponent<HorizontalLayoutGroup>();
            var serverInfoContainerSizeFitter = serverInfoContainerObject.AddComponent<ContentSizeFitter>();
            serverInfoContainerSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            serverInfoContainerSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var serverAddressObject = Main.Multiplayer.Factory.CreateDefaultGameObject(serverInfoContainerObject.transform);
            var serverAddressElement = serverAddressObject.AddComponent<LayoutElement>();
            serverAddressElement.preferredHeight = 40;
            var serverAddressBox = serverAddressObject.AddComponent<TextMeshProUGUI>();
            serverAddressBox.alignment = TextAlignmentOptions.Center;
            serverAddressBox.material = defaultMesh.Material;
            serverAddressBox.color = defaultMesh.Color;

            var settings = _multiplayerSettingsService.GetSettings();
            var endpointText = settings.HideServerAddress ? "***.***.***.***:****" : connectivity.Endpoint.ToString();
            serverAddressBox.SetText(endpointText);
        }

        public void UpdateCharacterOwnerDropdown(int characterIndex, int playerIndex, bool silent = false)
        {
            _mainThreadAccessor.Post(() =>
            {
                var characterContainer = CharactersInfoContainer.transform.GetChild(characterIndex);
                if (characterContainer == null)
                {
                    _logger.LogInformation("Unable to update character owner dropdow due to missing character container. Index={Index}", characterIndex);
                    return;
                }

                var dropdown = characterContainer.Find(CharacterOwnerObjectName);
                var dropdownObject = dropdown.transform.Find(UIFactory.DropdownGameObjectName);
                var tmpDropdown = dropdownObject.GetComponent<TMP_Dropdown>();
                if (silent)
                {
                    RemoveAllDropdownListeners(tmpDropdown);
                }

                tmpDropdown.value = playerIndex;
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

        public void UpdateCharacters(List<NetworkCharacter> characters, bool isHost)
        {
            if (GetContentOwnedObject() == null)
            {
                _logger.LogWarning("[{MethodName}] Content doesn't exist for the current owner. Owner={Owner}", nameof(UpdateCharacters), _activeOwner);
                return;
            }

            _mainThreadAccessor.Post(() =>
            {
                for (int characterIndex = 0; characterIndex < Main.MaxCharactersInParty; characterIndex++)
                {
                    var character = characters.Count > characterIndex ? characters[characterIndex] : null;
                    var sprite = string.IsNullOrEmpty(character?.Portrait) ? null : GetPortraitSprite(character.Portrait);
                    UpdateCharacterPortrait(characterIndex, sprite, isHost);
                }
            });
        }

        private void CreatePlayerObject(NetworkPlayer player)
        {
            var defaultMesh = Main.Multiplayer.Factory.GetDefaultMesh();
            var playerContainerObject = Main.Multiplayer.Factory.CreateDefaultGameObject(PlayersSectionContent.transform);
            playerContainerObject.name = PlayerContainerObjectName;
            var horizontal = playerContainerObject.AddComponent<HorizontalLayoutGroup>();
            horizontal.spacing = 6f;
            var playerContainerSizeFitter = playerContainerObject.AddComponent<ContentSizeFitter>();
            playerContainerSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            playerContainerSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            const int PreferredHeight = 28;
            var playerObject = Main.Multiplayer.Factory.CreateDefaultGameObject(playerContainerObject.transform);
            var playerElement = playerObject.AddComponent<LayoutElement>();
            playerElement.preferredHeight = PreferredHeight;
            playerObject.name = PlayerNameObjectName;
            var playerNameBox = playerObject.AddComponent<TextMeshProUGUI>();
            playerNameBox.alignment = TextAlignmentOptions.Center;
            playerNameBox.material = defaultMesh.Material;
            playerNameBox.color = defaultMesh.Color;
            playerNameBox.SetText(player.Name);
            if (!player.IsReady)
            {
                playerNameBox.fontStyle = FontStyles.Strikethrough;
            }

            if (player.ContentState.DiscrepantMods.Any() || player.ContentState.DiscrepantDLCs.Any())
            {
                CreatePlayerIcon("UI_QuestNotification_StampYellow", playerContainerObject, PreferredHeight, new ContentDiscrepancyTooltipTemplate(player));
            }
        }

        private void CreatePlayerIcon(string iconName, GameObject parent, int size, TooltipBaseTemplate template = null)
        {
            var iconObject = Main.Multiplayer.Factory.CreateDefaultGameObject(parent.transform);
            var layoutElement = iconObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = size;
            layoutElement.preferredWidth = size;
            var image = iconObject.AddComponent<Image>();
            var sprite = _resourceProvider.GetSprite(ResourceBundleProvider.UIBundleName, iconName);
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
            var players = networkPlayers.Select(x => x.Name).ToList();
            for (int characterIndex = 0; characterIndex < Main.MaxCharactersInParty; characterIndex++)
            {
                var characterContainer = CharactersInfoContainer.transform.GetChild(characterIndex);
                if (characterContainer == null)
                {
                    _logger.LogInformation("Unable to update character owner dropdow due to missing character container. Index={Index}", characterIndex);
                    return;
                }

                var dropdown = characterContainer.Find(CharacterOwnerObjectName);
                var dropdownObject = dropdown.transform.Find(UIFactory.DropdownGameObjectName);
                var tmpDropdown = dropdownObject.GetComponent<TMP_Dropdown>();
                RemoveAllDropdownListeners(tmpDropdown);
                tmpDropdown.onValueChanged.RemoveAllListeners();
                var selectedValue = tmpDropdown.value;
                tmpDropdown.ClearOptions();
                tmpDropdown.AddOptions(players);
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

        private void UpdateCharacterPortrait(int characterIndex, Sprite portraitSprite, bool isHost)
        {
            var characterContainer = CharactersInfoContainer.transform.GetChild(characterIndex);
            if (characterContainer == null)
            {
                _logger.LogInformation("Character doesn't exist. Index={Index}", characterIndex);
                return;
            }

            var portraitObject = characterContainer.Find(CharacterPortraitObjectName);
            var portraitImage = portraitObject.GetComponent<Image>();
            portraitImage.sprite = portraitSprite;
            portraitImage.color = portraitSprite == null ? Color.clear : Color.white;
            characterContainer.Find(CharacterOwnerObjectName).Find(UIFactory.DropdownGameObjectName).GetComponent<TMP_Dropdown>().interactable = isHost && portraitSprite != null;

            _logger.LogInformation("Updated character portrait. Index={Index}, SpriteName={SpriteName}", characterIndex, portraitSprite?.name);
        }

        private void OnOwnerDropdownChanged(TMP_Dropdown dropdown)
        {
            var player = dropdown.options.Count >= dropdown.value ? dropdown.options[dropdown.value].text : null;
            if (player == null)
            {
                _logger.LogWarning("Can't find selected player to assign character control");
                return;
            }

            var characterIndexComponent = dropdown.transform.parent?.GetComponent<CharacterIndexMonoBehaviour>();

            if (characterIndexComponent == null)
            {
                _logger.LogWarning($"Can't find ${nameof(CharacterIndexMonoBehaviour)} to assign character control");
                return;
            }

            _logger.LogInformation("Character owner changed. CharacterIndex={CharacterIndex}, Player={Player}", characterIndexComponent.CharacterIndex, player);
            OnCharacterOwnerChanged?.Invoke(characterIndexComponent.CharacterIndex, dropdown.value);
        }

        private Sprite GetPortraitSprite(string portraitName)
        {
            var portrait = _resourceProvider.GetSprite(ResourceBundleProvider.PortraitsBundleName, portraitName) ?? _resourceProvider.GetSprite(ResourceBundleProvider.PortraitsBundleName, ResourceBundleProvider.PlaceholderPortrait);
            if (portrait == null)
            {
                _logger.LogWarning("Unable to load character portrait. PortraitName={PortraitName}", portraitName);
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
    }
}
