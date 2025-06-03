using System;
using UnityEngine;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.MP.Entities;

namespace WOTRMultiplayer.Abstractions.UI.Windows
{
    public interface ILobbyWindow
    {
        GameObject MenuItem { get; set; }

        Func<NetworkGame> NetworkGame { get; set; }

        void Show(bool state);

        void AssignLobbyController(ILobbyWindowController controller);
    }
}
