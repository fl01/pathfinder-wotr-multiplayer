using System;
using System.Collections.Generic;
using System.Net;
using System.Numerics;
using Kingmaker.EntitySystem.Persistence;
using WOTRMultiplayer.MP.Entities;

namespace WOTRMultiplayer.Abstractions.MP
{
    public interface IMultiplayerParticipant
    {
        bool ReadyChanged();

        void MoveCharacter(string characterName, Vector3 destination, float delay, float orientation);

        bool IsActive { get; }

        bool IsInLobby { get; }

        void Dispose();

        Action<SaveInfo> OnStartGame { get; set; }

        Action<EndPoint> OnConnected { get; set; }

        NetworkGame CurrentGame { get; }

        Action<List<NetworkPlayer>> OnPlayersChanged { get; set; }

        bool CanControlCharacter(string characterName);

        void GameLoaded();
        void Pause();
        void Unpause();
        void OnAfterCueShow(string dialogName, string cueName, bool hasSystemAnswer);
    }
}
