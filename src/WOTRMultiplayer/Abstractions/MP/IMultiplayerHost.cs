using System;
using System.Collections.Generic;
using System.Net;
using Kingmaker.EntitySystem.Persistence;
using WOTRMultiplayer.MP;
using WOTRMultiplayer.MP.Entities;

namespace WOTRMultiplayer.Abstractions.MP
{
    public interface IMultiplayerHost
    {
        void Create(SaveInfo save, List<string> portraits, MultiplayerSettings multiplayerSettings);

        void Dispose();

        bool ReadyChanged();

        void NotifyGameCharactersChanged(SaveInfo save, List<string> portraits);

        void Start();

        void ChangeCharacterOwner(int characterIndex, int playerIndex);

        bool IsInLobby { get; }

        bool IsActive { get; }

        Action<List<NetworkPlayer>> OnPlayersChanged { get; set; }

        Action<EndPoint> OnConnected { get; set; }

        Action<SaveInfo> OnStartGame { get; set; }

        NetworkGame CurrentGame { get; }
    }
}
