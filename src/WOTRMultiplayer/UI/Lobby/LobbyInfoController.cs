using System;
using System.Collections.Generic;
using Kingmaker.UI;
using Kingmaker.UI.MVVM._VM.SaveLoad;
using TMPro;
using UnityEngine;
using WOTRMultiplayer.Extensions;

namespace WOTRMultiplayer.UI.Lobby
{
    public class LobbyInfoController
    {
        public const string LobbyScreenRootObjectName = "LobbyScreen";
        public const string LobbyContentObjectName = "LobbyContent";
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
            UpdatePortraits(value);
            var players = new List<string>()
            {
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
            };
            UpdatePlayers(players);
        }

        public void UpdatePlayers(List<string> players)
        {
            PlayersInfoContainer.CleanupAllChildren();
            var defaultMesh = Main.Multiplayer.ElementFactory.GetDefaultMesh();
            foreach (var playerName in players)
            {
                var playerInfoObject = Main.Multiplayer.ElementFactory.CreateDefaultGameObject(PlayersInfoContainer.transform);
                playerInfoObject.name = LobbyInfoController.PlayerContainerObjectName;
                playerInfoObject.AddComponent<HorizontalLayoutGroupWorkaround>();

                var player = Main.Multiplayer.ElementFactory.CreateDefaultGameObject(playerInfoObject.transform);
                player.name = LobbyInfoController.PlayerNameObjectName;
                var playerTitle = player.AddComponent<TextMeshProUGUI>();
                playerTitle.material = defaultMesh.Material;
                playerTitle.color = defaultMesh.Color;
                playerTitle.SetText(playerName);
            }
        }

        private void UpdatePortraits(SaveSlotVM saveSlotVM)
        {
            for (int i = 0; i < 6; i++)
            {
                var sprite = GetPortraitSprite(i, saveSlotVM);
                var specificCharacterContainer = CharactersInfoContainer.transform.GetChild(i);
                var portrait = specificCharacterContainer.Find(CharacterPortraitObjectName);
                var dropdown = specificCharacterContainer.Find(CharacterOwnerObjectName);
                var spriteRenderer = portrait.GetComponent<SpriteRenderer>();
                var portraitRect = portrait.GetComponent<RectTransform>();
                //spriteRenderer.size = portraitRect.sizeDelta;
                spriteRenderer.sprite = sprite;
                spriteRenderer.transform.localScale = new Vector3(60, 50, 0);
                portrait.SetPositionAndRotation(new Vector3(portrait.position.x - i * 25, portrait.position.y, portrait.position.z), portrait.transform.rotation);
            }

            CharactersInfoContainer.SetActive(false);
            CharactersInfoContainer.SetActive(true);


        }

        private Sprite GetPortraitSprite(int slot, SaveSlotVM saveSlotVM)
        {
            return saveSlotVM.PartyPortraits.Value.Count > slot ? saveSlotVM.PartyPortraits.Value[slot].Portrait : null;
        }
    }
}
