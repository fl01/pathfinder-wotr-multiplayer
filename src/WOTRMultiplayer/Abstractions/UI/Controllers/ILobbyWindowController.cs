using System;
using System.Collections.Generic;
using UnityEngine;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.UI.Menu;

namespace WOTRMultiplayer.Abstractions.UI.Controllers
{
    public interface ILobbyWindowController
    {
        void UpdatePlayers(List<NetworkPlayer> players);

        void InitializeContent(LobbyWindowOwner owner, Transform parent);
        void ResetData();

        void UpdateServerInfo(NetworkGameConnectivity connectivity);
        void UpdateCharacters(List<NetworkCharacter> characters, bool isHost);
        void UpdateCharacterOwnerDropdown(int characterIndex, int playerIndex, bool silent = false);
        void SetActiveOwner(LobbyWindowOwner owner);

        void ResetOwnerContent(LobbyWindowOwner owner);

        Action<int, int> OnCharacterOwnerChanged { get; set; }
    }
}
