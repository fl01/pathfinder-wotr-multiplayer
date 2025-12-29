using System;
using System.Collections.Generic;
using UnityEngine;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Entities;

namespace WOTRMultiplayer.Abstractions.UI.Windows
{
    public interface ILobbyWindow
    {
        GameObject MenuItem { get; set; }

        Func<NetworkGameConnectivity> GetGameConnectivity { get; set; }

        Func<List<NetworkPlayer>> GetPlayers { get; set; }

        Func<bool> GetIsHost { get; set; }

        Func<List<NetworkCharacter>> GetCharacters { get; set; }

        void Show(bool state);

        void AssignLobbyController(ILobbyWindowController controller);
    }
}
