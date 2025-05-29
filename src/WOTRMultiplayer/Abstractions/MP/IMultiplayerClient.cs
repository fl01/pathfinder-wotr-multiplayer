using System;
using WOTRMultiplayer.MP;
using WOTRMultiplayer.MP.Entities;

namespace WOTRMultiplayer.Abstractions.MP
{
    public interface IMultiplayerClient
    {
        void Dispose();

        JoinLobbyResult Join(string address, MultiplayerSettings settings);

        bool ReadyChanged();

        bool IsActive { get; }

        bool IsConnecting { get; }

        Action<string> OnNetworkError { get; set; }

        Action OnConnected { get; set; }

        Action OnDisconnected { get; set; }
    }
}
