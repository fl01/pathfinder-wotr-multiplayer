using System;
using System.Collections.Generic;
using System.Net;
using System.Numerics;
using Kingmaker.EntitySystem.Persistence;
using WOTRMultiplayer.MP.Entities;

namespace WOTRMultiplayer.Abstractions.MP
{
    public interface IMultiplayerHost
    {
        void Create(SaveInfo save, List<NetworkCharacter> characters);

        void Dispose();

        bool ReadyChanged();

        void UpdateSaveGame(SaveInfo save, List<NetworkCharacter> characters);

        void Start();

        void ChangeCharacterOwner(int characterIndex, int playerIndex);
        void MoveCharacter(string characterName, Vector3 destination, float delay, float orientation);

        bool IsInLobby { get; }

        bool IsActive { get; }

        Action<List<NetworkPlayer>> OnPlayersChanged { get; set; }

        Action<EndPoint> OnConnected { get; set; }

        Action<SaveInfo> OnStartGame { get; set; }

        NetworkGame CurrentGame { get; }
    }
}
