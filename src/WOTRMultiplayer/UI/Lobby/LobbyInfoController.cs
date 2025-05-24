using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker.UI;
using Kingmaker.UI.MVVM._VM.SaveLoad;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.Unity;

namespace WOTRMultiplayer.UI.Lobby
{
    public class LobbyInfoController
    {
        public const string LobbyScreenRootObjectName = "LobbyScreen";
        public const string LobbyContentObjectName = "LobbyContent";
        public const string PlayersSectionObjectName = "PlayersSectionTitle";
        public const string PlayersInfoContainerObjectName = "PlayersInfoContainer";
        public const string PlayerContainerObjectName = "PlayerContainer";
        public const string PlayerNameObjectName = "PlayerName";
        public const string CharactersInfoContainerObjectName = "CharactersInfoContainer";
        public const string SpecificCharacterContainerObjectName = "SpecificCharacterContainer";
        public const string CharacterPortraitObjectName = "CharacterPortrait";
        public const string CharacterOwnerObjectName = "CharacterOwner";

        private readonly GameObject _content;
        private GameObject PlayersInfoContainer => _content.transform.Find(LobbyContentObjectName).Find(PlayersInfoContainerObjectName).gameObject;
        private GameObject CharactersInfoContainer => _content.transform.Find(LobbyContentObjectName).Find(CharactersInfoContainerObjectName).gameObject;
        public LobbyInfoController(GameObject content)
        {
            _content = content;
        }

        public void SaveSlotSelected(SaveSlotVM value)
        {
            Logging.Logger.Info($"Selected SaveSlo={value.SaveName.Value}");
            var players = new List<string>()
            {
                Guid.NewGuid().ToString().Split('-').First(),
                Guid.NewGuid().ToString().Split('-').First(),
                Guid.NewGuid().ToString().Split('-').First(),
                Guid.NewGuid().ToString().Split('-').First(),
            };
            UpdatePlayers(players);
            UpdateCharacters(value, players);
        }

        public void UpdatePlayers(List<string> players)
        {
            PlayersInfoContainer.CleanupAllChildren();
            var defaultMesh = Main.Multiplayer.Factory.GetDefaultMesh();
            foreach (var playerName in players)
            {
                var playerInfoObject = Main.Multiplayer.Factory.CreateDefaultGameObject(PlayersInfoContainer.transform);
                playerInfoObject.name = LobbyInfoController.PlayerContainerObjectName;
                playerInfoObject.AddComponent<HorizontalLayoutGroupWorkaround>();

                var player = Main.Multiplayer.Factory.CreateDefaultGameObject(playerInfoObject.transform);
                player.name = LobbyInfoController.PlayerNameObjectName;
                var playerTitle = player.AddComponent<TextMeshProUGUI>();
                playerTitle.material = defaultMesh.Material;
                playerTitle.color = defaultMesh.Color;
                playerTitle.SetText(playerName);
            }
        }

        private void UpdateCharacters(SaveSlotVM saveSlotVM, List<string> players)
        {
            for (int characterIndex = 0; characterIndex < UIFactory.GetMaxCharactersCount(); characterIndex++)
            {
                var sprite = GetPortraitSprite(characterIndex, saveSlotVM);
                var specificCharacterContainer = CharactersInfoContainer.transform.GetChild(characterIndex);
                var portrait = specificCharacterContainer.Find(CharacterPortraitObjectName);
                var dropdown = specificCharacterContainer.Find(CharacterOwnerObjectName);
                var img = portrait.GetComponent<Image>();
                img.sprite = sprite;
                img.color = sprite == null ? Color.clear : Color.white;
                // TBD test stuff
                var dropdownObject = dropdown.transform.Find(UIFactory.DropdownGameObjectName);
                var tmpDropdown = dropdownObject.GetComponent<TMP_Dropdown>();
                tmpDropdown.onValueChanged.RemoveAllListeners();
                tmpDropdown.ClearOptions();
                tmpDropdown.AddOptions(players);
                tmpDropdown.onValueChanged.AddListener(index => OnCharacterOwnerChanged(tmpDropdown));
            }
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
