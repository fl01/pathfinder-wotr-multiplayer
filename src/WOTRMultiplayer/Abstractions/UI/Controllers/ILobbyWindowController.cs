using System;
using System.Collections.Generic;
using UnityEngine;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.UI.Lobby;

namespace WOTRMultiplayer.Abstractions.UI.Controllers
{
    public interface ILobbyWindowController
    {
        void UpdatePlayers(List<NetworkPlayer> playersList);

        void InitializeContent(LobbyWindowOwner owner, Transform parent, bool canUseCharacterDropdown);
        void ResetData();

        void UpdateServerInfo(string serverAddress);
        void UpdatePortraits(List<string> portraits);
        void UpdateCharacterOwnerDropdown(int characterIndex, int playerIndex);
        void SetActiveOwner(LobbyWindowOwner owner);

        void ResetOwnerContent(LobbyWindowOwner owner);

        Action<int, int> OnCharacterOwnerChanged { get; set; }
    }
}
