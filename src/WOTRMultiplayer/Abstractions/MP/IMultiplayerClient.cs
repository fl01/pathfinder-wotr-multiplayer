using System;
using System.Collections.Generic;
using System.Net;
using Kingmaker.EntitySystem.Persistence;
using WOTRMultiplayer.MP;
using WOTRMultiplayer.MP.Entities;

namespace WOTRMultiplayer.Abstractions.MP
{
    public interface IMultiplayerClient
    {
        void Dispose();

        ConnectLobbyResult Connect(string address, MultiplayerSettings settings);

        bool ReadyChanged();

        bool IsActive { get; }

        bool IsInLobby { get; }

        bool IsConnecting { get; }

        Action<string> OnNetworkError { get; set; }

        Action<EndPoint> OnConnected { get; set; }

        Action<List<NetworkPlayer>> OnPlayersChanged { get; set; }

        Action<List<string>> OnGameCharactersChanged { get; set; }

        Action<int, int> OnCharacterOwnerChanged { get; set; }

        Action<SaveInfo> OnStartGame { get; set; }
    }
}
