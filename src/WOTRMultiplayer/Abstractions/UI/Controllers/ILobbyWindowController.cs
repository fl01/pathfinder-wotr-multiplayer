using System.Collections.Generic;
using Kingmaker.UI.MVVM._VM.SaveLoad;
using UnityEngine;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.UI.Lobby;

namespace WOTRMultiplayer.Abstractions.UI.Controllers
{
    public interface ILobbyWindowController
    {
        void UpdatePlayers(List<NetworkPlayer> playersList);

        void InitializeContent(LobbyWindowOwner owner, Transform parent);
        void Reset();
        void UpdateServerInfo(string serverAddress);
        void UpdateCharacters(SaveSlotVM value);
        void SetActiveOwner(LobbyWindowOwner owner);
    }
}
