using System.Collections.Generic;
using System.Net;
using Kingmaker.EntitySystem.Persistence;

namespace WOTRMultiplayer.MP.Entities
{
    public class NetworkGame
    {
        public EndPoint Endpoint { get; set; }

        public List<string> Portraits { get; set; } = [];

        public NetworkGameStage Stage { get; set; }

        public List<NetworkPlayer> Players { get; set; } = [];

        public List<NetworkCharacterOwner> CharacterOwners { get; set; } = [];

        public SaveInfo Save { get; set; }

        public NetworkGame(SaveInfo save)
        {
            Save = save;
            Stage = NetworkGameStage.Lobby;
        }

        public void Reset()
        {
            Portraits.Clear();
            Players.Clear();
            CharacterOwners.Clear();
            Save = null;
            Endpoint = null;
            Stage = NetworkGameStage.None;
        }
    }
}
