using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Kingmaker.UI.MVVM._VM.SaveLoad;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.Unity;

namespace WOTRMultiplayer.UI.Lobby
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
        private readonly ConcurrentDictionary<LobbyWindowOwner, GameObject> _contents = new();
        private LobbyWindowOwner _activeOwner;

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
            IUIFactory uIFactory)
        {
            _logger = logger;
            _uIFactory = uIFactory;
            _mainThreadAccessor = mainThreadAccessor;
        }

        public void InitializeContent(LobbyWindowOwner owner, Transform parent)
        {
            _logger.LogInformation("Initialize content. Owner={owner}", owner);

            if (!_contents.TryGetValue(owner, out var content) && content != null)
            {
                _logger.LogError("Lobby content still exists on the scene. Owner={owner}", owner);
                return;
            }

            var canUseDropdown = owner == LobbyWindowOwner.HostMenu;
            var lobbyContent = _uIFactory.CreateLobbyWindowContent(parent, canUseDropdown);
            lobbyContent.SetActive(false);
            _contents.TryAdd(owner, lobbyContent);
        }

        public void UpdatePlayers(List<NetworkPlayer> players)
        {
            if (GetContentOwnedObject() == null)
            {
                return;
            }

            _mainThreadAccessor.MainThreadQueue.Enqueue(() =>
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

        public void UpdateServerInfo(string serverAddress)
        {
            if (GetContentOwnedObject() == null)
            {
                return;
            }

            GetContentOwnedObject().SetActive(true);

            ServerInfoSectionContent.CleanupAllChildren();

            var defaultMesh = Main.Multiplayer.Factory.GetDefaultMesh();
            var serverInfoContainerObject = Main.Multiplayer.Factory.CreateDefaultGameObject(ServerInfoSectionContent.transform);
            serverInfoContainerObject.name = LobbyWindowController.PlayerContainerObjectName;
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
            serverAddressBox.SetText(serverAddress);
        }

        private void CreatePlayerObject(NetworkPlayer player)
        {
            var defaultMesh = Main.Multiplayer.Factory.GetDefaultMesh();
            var playerContainerObject = Main.Multiplayer.Factory.CreateDefaultGameObject(PlayersSectionContent.transform);
            playerContainerObject.name = LobbyWindowController.PlayerContainerObjectName;
            playerContainerObject.AddComponent<HorizontalLayoutGroup>();
            var playerContainerSizeFitter = playerContainerObject.AddComponent<ContentSizeFitter>();
            playerContainerSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            playerContainerSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var playerObject = Main.Multiplayer.Factory.CreateDefaultGameObject(playerContainerObject.transform);
            var playerElement = playerObject.AddComponent<LayoutElement>();
            playerElement.preferredHeight = 40;
            playerObject.name = LobbyWindowController.PlayerNameObjectName;
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
            for (int characterIndex = 0; characterIndex < UIFactory.GetMaxCharactersCount(); characterIndex++)
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
                tmpDropdown.onValueChanged.RemoveAllListeners();
                tmpDropdown.ClearOptions();
                tmpDropdown.AddOptions(players);
                tmpDropdown.onValueChanged.RemoveAllListeners();
                tmpDropdown.onValueChanged.AddListener(index => OnCharacterOwnerChanged(tmpDropdown));
            }
        }

        public void UpdateCharacters(SaveSlotVM saveSlotVM)
        {
            for (int characterIndex = 0; characterIndex < UIFactory.GetMaxCharactersCount(); characterIndex++)
            {
                var sprite = GetPortraitSprite(characterIndex, saveSlotVM);
                UpdateCharacterPortrait(characterIndex, sprite);
            }
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

        private void OnCharacterOwnerChanged(TMP_Dropdown dropdown)
        {
            var player = dropdown.options.Count >= dropdown.value ? dropdown.options[dropdown.value].text : null;
            if (player == null)
            {
                _logger.LogWarning("Can't find selected player to assign character control");
                return;
            }

            var characterIndexComponent = dropdown.transform.parent?.GetComponent<CharacterIndexMonoBehavior>();

            if (characterIndexComponent == null)
            {
                _logger.LogWarning($"Can't find ${nameof(CharacterIndexMonoBehavior)} to assign character control");
                return;
            }

            _logger.LogInformation("Character owner changed. CharacterIndex={characterIndex}, Player={player}", characterIndexComponent.CharacterIndex, player);
        }

        private Sprite GetPortraitSprite(int slot, SaveSlotVM saveSlotVM)
        {
            return saveSlotVM.PartyPortraits.Value.Count > slot ? saveSlotVM.PartyPortraits.Value[slot].Portrait : null;
        }

        public void ResetData()
        {
            var current = GetContentOwnedObject();
            var playerSection = PlayersSectionContent;
            var serverSection = ServerInfoSectionContent;
            _mainThreadAccessor.MainThreadQueue.Enqueue(() =>
            {
                current?.SetActive(false);
                playerSection?.CleanupAllChildren();
                serverSection?.CleanupAllChildren();
            });
        }

        public void SetActiveOwner(LobbyWindowOwner owner)
        {
            _activeOwner = owner;
        }

        public GameObject GetContentOwnedObject()
        {
            if (!_contents.TryGetValue(_activeOwner, out var content) || content == null)
            {
                _logger.LogWarning("Content doesn't exist for the current owner. Owner={owner}", _activeOwner);
                return null;
            }

            return content;
        }

        public void ResetOwner(LobbyWindowOwner owner)
        {
            _logger.LogInformation("Reset owner content objects. Owner={owner}", owner);
            _contents.TryRemove(owner, out var _);
        }
    }
}
