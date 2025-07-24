using System;
using System.Collections.Generic;
using WOTRMultiplayer.MP.Entities;

namespace WOTRMultiplayer.Abstractions.MP
{
    public interface IMultiplayerClient : IMultiplayerActor
    {
        ConnectLobbyResult Connect(string address);

        bool IsConnecting { get; }

        Action<string> OnNetworkError { get; set; }

        Action<List<NetworkCharacterOwnership>> OnGameCharactersChanged { get; set; }

        Action<int, int> OnCharacterOwnerChanged { get; set; }
    }
}
