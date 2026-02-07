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
        private readonly IUIFactory _uIFactory;
        private readonly IMainThreadAccessor _mainThreadAccessor;
        private readonly IResourceProvider _resourceProvider;
        private readonly IMultiplayerSettingsService _multiplayerSettingsService;
        private readonly ConcurrentDictionary<LobbyWindowOwner, GameObject> _contents = new();
        private LobbyWindowOwner _activeOwner;

        private readonly List<IDisposable> _disposables = [];

        public Action<NetworkCharacter, NetworkPlayer> OnCharacterOwnerChanged { get; set; }

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
            serverAddressBox.material = Main.Multiplayer.Factory.DefaultTextMesh.Material;
            serverAddressBox.color = Main.Multiplayer.Factory.DefaultTextMesh.Color;

            var settings = _multiplayerSettingsService.GetSettings();
            var endpointText = settings.HideServerAddress ? "***.***.***.***:****" : connectivity.Endpoint.ToString();
            serverAddressBox.SetText(endpointText);
        }

        public void UpdateCharacterOwnerDropdown(NetworkCharacter character, bool silent = false)
        {
            _mainThreadAccessor.Post(() =>
            {
                if (_activeOwner != LobbyWindowOwner.EscMenu)
                {
                    return;
                }

                var characterContainer = FindCharacterContainer(character);
                if (characterContainer == null)
                {
                    _logger.LogWarning("Unable to update character owner dropdow due to missing character container. CharacterName={CharacterName}, CharacterId={CharacterId}", character.Name, character.UnitId);
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
                    var sprite = string.IsNullOrEmpty(character?.Portrait) ? null : GetPortraitSprite(character.Portrait);
                    UpdateCharacter(characterIndex, character, sprite, isDropdownInteractable);

                    if (character != null && character.Owner != null)
                    {
                        UpdateCharacterOwnerDropdown(character, silent: true);
                    }
                }
            });
        }

        private Transform FindCharacterContainer(NetworkCharacter character)
        {
            foreach (Transform child in CharactersInfoContainer.transform)
            {
                var dropdownCharacter = child.Find(CharacterOwnerObjectName)?.GetComponent<CharacterDataBehaviour>()?.Character;
                if (dropdownCharacter != null && (!string.IsNullOrEmpty(character.UnitId) && string.Equals(dropdownCharacter.UnitId, character.UnitId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(dropdownCharacter.Name, character.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    return child;
                }
            }

            return null;
        }

        private void CreatePlayerObject(NetworkPlayer player)
        {
            var defaultMesh = Main.Multiplayer.Factory.DefaultTextMesh;
            var playerContainerObject = Main.Multiplayer.Factory.CreateDefaultGameObject(PlayersSectionContent.transform);
            playerContainerObject.name = PlayerContainerObjectName;
            var horizontal = playerContainerObject.AddComponent<HorizontalLayoutGroup>();
            horizontal.spacing = 6f;
            var playerContainerSizeFitter = playerContainerObject.AddComponent<ContentSizeFitter>();
            playerContainerSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            playerContainerSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            const int PreferredHeight = 28;
            var gameVersionObject = Main.Multiplayer.Factory.CreateDefaultGameObject(playerContainerObject.transform);
            var gameVersionElement = gameVersionObject.AddComponent<LayoutElement>();
            gameVersionElement.preferredHeight = PreferredHeight;
            var gameVersionBox = gameVersionObject.AddComponent<TextMeshProUGUI>();
            gameVersionBox.alignment = TextAlignmentOptions.Center;
            gameVersionBox.material = defaultMesh.Material;
            gameVersionBox.color = defaultMesh.Color;
            gameVersionBox.SetText($"[{player.ContentState.GameVersion}]");

            var playerObject = Main.Multiplayer.Factory.CreateDefaultGameObject(playerContainerObject.transform);
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
                CreatePlayerIcon("UI_journal_iconok_new2", playerContainerObject, PreferredHeight, null);
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
            var sprite = _resourceProvider.GetSprite(WellKnownResourceBundles.UI, iconName);
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

        private Sprite GetPortraitSprite(string portraitName)
        {
            var portrait = _resourceProvider.GetSprite(WellKnownResourceBundles.Portraits, portraitName) ?? _resourceProvider.GetSprite(WellKnownResourceBundles.Portraits, PlaceholderPortrait);
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
