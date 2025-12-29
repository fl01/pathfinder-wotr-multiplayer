using System;
using System.Collections.Generic;
using WOTRMultiplayer.Entities;

namespace WOTRMultiplayer.Abstractions
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
