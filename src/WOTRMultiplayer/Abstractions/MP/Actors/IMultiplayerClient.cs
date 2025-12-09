using System;
using System.Collections.Generic;
using WOTRMultiplayer.MP.Entities;

namespace WOTRMultiplayer.Abstractions.MP.Actors
{
    public interface IMultiplayerClient : IMultiplayerActor
    {
        AddressParseResult Connect(string address);

        bool IsConnecting { get; }

        Action OnNetworkError { get; set; }

        Action<List<NetworkCharacter>> OnGameCharactersChanged { get; set; }

        Action<int, int> OnCharacterOwnerChanged { get; set; }

        void OnBeforeTryRollRandomEncounter();
    }
}
