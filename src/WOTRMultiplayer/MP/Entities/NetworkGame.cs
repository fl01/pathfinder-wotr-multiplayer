using System.Collections.Generic;

namespace WOTRMultiplayer.MP.Entities
{
    public class NetworkGame
    {
        public List<string> Portraits { get; set; } = [];

        public NetworkGameStatus Status { get; set; }

        public List<NetworkPlayer> Players { get; set; } = [];

        public string SavePath { get; set; }

        public NetworkGame(string savePath)
        {
            SavePath = savePath;
            Status = NetworkGameStatus.Lobby;
        }

        public void Reset()
        {
            Portraits.Clear();
            Players.Clear();
            SavePath = null;
            Status = NetworkGameStatus.None;
        }
    }
}
