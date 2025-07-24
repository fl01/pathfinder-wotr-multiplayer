using System.Collections.Generic;

namespace WOTRMultiplayer.MP.Entities
{
    public class NetworkGame
    {
        public long LocalPlayerId { get; set; }

        public NetworkGameConnectivity Connectivity { get; set; }

        public NetworkGameStage Stage { get; set; }

        public List<NetworkPlayer> Players { get; set; } = [];

        public List<NetworkCharacterOwnership> Characters { get; set; } = [];

        public NetworkCombat Combat { get; set; }

        public NetworkDialog Dialog { get; set; }

        public string SaveFilePath { get; set; }

        public NetworkGame(string saveFilePath)
        {
            SaveFilePath = saveFilePath;
            Stage = NetworkGameStage.Lobby;
        }

        public void Reset()
        {
            LocalPlayerId = 0; // -1 host << 0 default << 1+ clients
            Players.Clear();
            Characters.Clear();
            SaveFilePath = null;
            Connectivity = null;
            Stage = NetworkGameStage.None;
            Dialog = null;
        }
    }
}
