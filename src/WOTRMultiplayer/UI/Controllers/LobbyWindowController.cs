using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.Unity;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.UI.Menu;
using WOTRMultiplayer.Unity.Behaviours;

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
        private readonly ConcurrentDictionary<LobbyWindowOwner, GameObject> _contents = new();
        private LobbyWindowOwner _activeOwner;

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
            IUIFactory uIFactory)
        {
            _logger = logger;
            _uIFactory = uIFactory;
            _mainThreadAccessor = mainThreadAccessor;
            _resourceProvider = resourceProvider;
        }

        public void InitializeContent(LobbyWindowOwner owner, Transform parent, bool canUseCharacterDropdown)
        {
            _logger.LogInformation("Initialize content. Owner={owner}", owner);

            if (_contents.TryGetValue(owner, out var content) && content != null)
            {
                _logger.LogWarning("Lobby content still exists on the scene, skipping initialization. Owner={owner}", owner);
                return;
            }

            var canUseDropdown = canUseCharacterDropdown;
            var lobbyContent = _uIFactory.CreateLobbyWindowContent(parent, canUseDropdown);
            lobbyContent.SetActive(false);
            _contents.TryAdd(owner, lobbyContent);
            _logger.LogInformation("Content has been created. Owner={owner}", owner);
        }

        public void UpdatePlayers(List<NetworkPlayer> players)
        {
            if (GetContentOwnedObject() == null)
            {
                return;
            }

            _mainThreadAccessor.Enqueue(() =>
            {
                _logger.LogInformation("Updating player list. PlayersCount={playersCount}", players.Count);
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
            serverAddressBox.SetText(connectivity.Endpoint.ToString());
        }

        public void UpdateCharacterOwnerDropdown(int characterIndex, int playerIndex, bool silent = false)
        {
            _mainThreadAccessor.Enqueue(() =>
            {
                var characterContainer = CharactersInfoContainer.transform.GetChild(characterIndex);
                if (characterContainer == null)
                {
                    _logger.LogInformation("Unable to update character owner dropdow due to missing character container. Index={characterIndex}", characterIndex);
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
            _mainThreadAccessor.Enqueue(() =>
            {
                current?.SetActive(false);
                playerSection?.CleanupAllChildren();
                serverSection?.CleanupAllChildren();
                UpdateCharacters([]);
            });
        }

        public void SetActiveOwner(LobbyWindowOwner owner)
        {
            if (_activeOwner != owner)
            {
                _logger.LogInformation("Changing current owner. Previous={prevOwner}, NewOwner={newOwner}", _activeOwner, owner);
                _activeOwner = owner;
            }
        }

        public void ResetOwnerContent(LobbyWindowOwner owner)
        {
            _logger.LogInformation("Reset owner content objects. Owner={owner}", owner);
            _contents.TryRemove(owner, out var _);
        }

        public void UpdateCharacters(List<NetworkCharacterOwnership> characters)
        {
            if (GetContentOwnedObject() == null)
            {
                _logger.LogWarning("[{methodName}] Content doesn't exist for the current owner. Owner={owner}", nameof(UpdateCharacters), _activeOwner);
                return;
            }

            _mainThreadAccessor.Enqueue(() =>
            {
                for (int characterIndex = 0; characterIndex < Main.MaxCharacters; characterIndex++)
                {
                    var character = characters.Count > characterIndex ? characters[characterIndex] : null;
                    var sprite = string.IsNullOrEmpty(character?.Portrait) ? null : GetPortraitSprite(character.Portrait);
                    UpdateCharacterPortrait(characterIndex, sprite);
                }
            });
        }

        private void CreatePlayerObject(NetworkPlayer player)
        {
            var defaultMesh = Main.Multiplayer.Factory.GetDefaultMesh();
            var playerContainerObject = Main.Multiplayer.Factory.CreateDefaultGameObject(PlayersSectionContent.transform);
            playerContainerObject.name = PlayerContainerObjectName;
            playerContainerObject.AddComponent<HorizontalLayoutGroup>();
            var playerContainerSizeFitter = playerContainerObject.AddComponent<ContentSizeFitter>();
            playerContainerSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            playerContainerSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var playerObject = Main.Multiplayer.Factory.CreateDefaultGameObject(playerContainerObject.transform);
            var playerElement = playerObject.AddComponent<LayoutElement>();
            playerElement.preferredHeight = 40;
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
        }

        private void UpdateCharacterOwnerDropdown(List<NetworkPlayer> networkPlayers)
        {
            var players = networkPlayers.Select(x => x.Name).ToList();
            for (int characterIndex = 0; characterIndex < Main.MaxCharacters; characterIndex++)
            {
                var characterContainer = CharactersInfoContainer.transform.GetChild(characterIndex);
                if (characterContainer == null)
                {
                    _logger.LogInformation("Unable to update character owner dropdow due to missing character container. Index={characterIndex}", characterIndex);
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

        private void UpdateCharacterPortrait(int characterIndex, Sprite portraitSprite)
        {
            var characterContainer = CharactersInfoContainer.transform.GetChild(characterIndex);
            if (characterContainer == null)
            {
                _logger.LogInformation("Character doesn't exist. Index={characterIndex}", characterIndex);
                return;
            }

            var portraitObject = characterContainer.Find(CharacterPortraitObjectName);
            var portraitImage = portraitObject.GetComponent<Image>();
            portraitImage.sprite = portraitSprite;
            portraitImage.color = portraitSprite == null ? Color.clear : Color.white;
            _logger.LogInformation("Updated character portrait. Index={characterIndex}, SpriteName={spriteName}", characterIndex, portraitSprite?.name);
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

            _logger.LogInformation("Character owner changed. CharacterIndex={characterIndex}, Player={player}", characterIndexComponent.CharacterIndex, player);
            OnCharacterOwnerChanged?.Invoke(characterIndexComponent.CharacterIndex, dropdown.value);
        }

        private Sprite GetPortraitSprite(string portraitName)
        {
            var portrait = _resourceProvider.GetPortrait(portraitName);
            if (portrait == null)
            {
                _logger.LogWarning("Unable to load character portrait. PortraitName={portraitName}", portraitName);
            }

            return portrait;
        }

        private GameObject GetContentOwnedObject([CallerMemberName] string callerName = "")
        {
            if (!_contents.TryGetValue(_activeOwner, out var content) || content == null)
            {
                _logger.LogWarning("[{callerName}] Content doesn't exist for the current owner. Owner={owner}", callerName, _activeOwner);
                return null;
            }

            return content;
        }
    }
}
