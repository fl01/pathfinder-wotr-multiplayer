using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker.UI.MVVM._VM.SaveLoad;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.Unity;

namespace WOTRMultiplayer.UI.Lobby
{
    public class LobbyInfoController
    {
        public const string LobbyScreenRootObjectName = "LobbyScreen";
        public const string LobbyContentObjectName = "LobbyContent";

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

        private readonly GameObject _content;
        private GameObject PlayersSectionContent => _content.transform
            .Find(LobbyContentObjectName)
            .Find(PlayersSectionObjectName)
            .Find(PlayersSectionContentObjectName).gameObject;

        private GameObject CharactersInfoContainer => _content.transform
            .Find(LobbyContentObjectName)
            .Find(CharactersSectionObjectName)
            .Find(CharactersSectionContentObjectName).gameObject;

        public LobbyInfoController(GameObject content)
        {
            _content = content;
        }

        public void SaveSlotSelected(SaveSlotVM value)
        {
            Logging.Logger.Info($"Selected SaveSlo={value.SaveName.Value}");
            var rnd = new System.Random();
            //UpdatePlayers(players);
            UpdateCharacters(value);
        }

        public void UpdatePlayers(List<NetworkPlayer> players)
        {
            PlayersSectionContent.CleanupAllChildren();
            foreach (var player in players)
            {
                CreatePlayerObject(player);
            }
        }

        private void CreatePlayerObject(NetworkPlayer player)
        {
            var defaultMesh = Main.Multiplayer.Factory.GetDefaultMesh();
            var playerContainerObject = Main.Multiplayer.Factory.CreateDefaultGameObject(PlayersSectionContent.transform);
            playerContainerObject.name = LobbyInfoController.PlayerContainerObjectName;
            var playerContainerHorizontal = playerContainerObject.AddComponent<HorizontalLayoutGroup>();
            var playerContainerSizeFitter = playerContainerObject.AddComponent<ContentSizeFitter>();
            playerContainerSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            playerContainerSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var playerObject = Main.Multiplayer.Factory.CreateDefaultGameObject(playerContainerObject.transform);
            var playerElement = playerObject.AddComponent<LayoutElement>();
            playerElement.preferredHeight = 40;
            playerObject.name = LobbyInfoController.PlayerNameObjectName;
            var playerNameBox = playerObject.AddComponent<TextMeshProUGUI>();
            playerNameBox.alignment = TextAlignmentOptions.Center;
            playerNameBox.material = defaultMesh.Material;
            playerNameBox.color = defaultMesh.Color;
            playerNameBox.SetText(player.Name);
            if (player.Status == NetworkPlayerReadinessStatus.NotReady)
            {
                playerNameBox.fontStyle = FontStyles.Strikethrough;
            }
        }

        private void UpdateCharacters(SaveSlotVM saveSlotVM)
        {
            for (int characterIndex = 0; characterIndex < UIFactory.GetMaxCharactersCount(); characterIndex++)
            {
                var sprite = GetPortraitSprite(characterIndex, saveSlotVM);
                UpdateCharacterPortrait(characterIndex, sprite);
                //// TBD test stuff
                //var dropdownObject = dropdown.transform.Find(UIFactory.DropdownGameObjectName);
                //var tmpDropdown = dropdownObject.GetComponent<TMP_Dropdown>();
                //tmpDropdown.onValueChanged.RemoveAllListeners();
                //tmpDropdown.ClearOptions();
                //tmpDropdown.AddOptions(players);
                //tmpDropdown.onValueChanged.AddListener(index => OnCharacterOwnerChanged(tmpDropdown));
            }
        }

        private void UpdateCharacterPortrait(int characterIndex, Sprite portraitSprite)
        {
            var specificCharacterContainer = CharactersInfoContainer.transform.GetChild(characterIndex);
            if (specificCharacterContainer == null)
            {
                Logging.Logger.Info($"Character doesn't exist. Index={characterIndex}");
            }

            var portrait = specificCharacterContainer.Find(CharacterPortraitObjectName);
            var dropdown = specificCharacterContainer.Find(CharacterOwnerObjectName);
            var img = portrait.GetComponent<Image>();
            img.sprite = portraitSprite;
            img.color = portraitSprite == null ? Color.clear : Color.white;
            Logging.Logger.Info($"Updated character portrait. Index={characterIndex}, SpriteName={portraitSprite?.name}");
        }

        private void OnCharacterOwnerChanged(TMP_Dropdown dropdown)
        {
            var player = dropdown.options.Count >= dropdown.value ? dropdown.options[dropdown.value].text : null;
            if (player == null)
            {
                Logging.Logger.Warning("Can't find selected player to assign character control");
                return;
            }

            var characterIndexComponent = dropdown.transform.parent?.GetComponent<CharacterIndexMonoBehavior>();

            if (characterIndexComponent == null)
            {
                Logging.Logger.Warning($"Can't find ${nameof(CharacterIndexMonoBehavior)} to assign character control");
                return;
            }

            Logging.Logger.Info($"Character owner changed. CharacterIndex={characterIndexComponent.CharacterIndex}, Player={player}");
        }

        private Sprite GetPortraitSprite(int slot, SaveSlotVM saveSlotVM)
        {
            return saveSlotVM.PartyPortraits.Value.Count > slot ? saveSlotVM.PartyPortraits.Value[slot].Portrait : null;
        }
    }
}
