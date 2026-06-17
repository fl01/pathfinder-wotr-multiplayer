using System;
using System.Collections.Generic;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.UI.Windows;

namespace WOTRMultiplayer.Abstractions.UI.Windows
{
    public interface ILobbyWindow
    {
        Func<GameConnectivity> GetGameConnectivity { get; set; }

        Func<List<NetworkPlayer>> GetPlayers { get; set; }

        Func<bool> GetIsHost { get; set; }

        Func<List<NetworkCharacter>> GetCharacters { get; set; }

        bool IsVisible { get; }

        void Close();

        void Show();

        ILobbyWindow WithController(ILobbyWindowController controller);

        ILobbyWindow WithShowHandler(Action onShow);

        ILobbyWindow WithCloseHandler(Action onClose);

        ILobbyWindow Initialize(LobbyWindowOwner lobbyWindowOwner);
    }
}
