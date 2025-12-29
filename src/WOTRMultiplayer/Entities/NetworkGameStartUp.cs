using System.Collections.Generic;
using WOTRMultiplayer.Entities.NewGame;

namespace WOTRMultiplayer.Entities
{
    public class NetworkGameStartUp
    {
        public bool IsNewGameSequence { get; set; }

        public List<NetworkCharacter> Characters { get; set; } = [];

        public NetworkNewGameSequencePhaseType PhaseType { get; set; }

        public HashSet<long> PlayerReadiness { get; set; } = [];

        public string SavePath { get; set; }

        public NetworkGameStartUp(string savePath)
        {
            SavePath = savePath;
            IsNewGameSequence = string.IsNullOrEmpty(savePath);
        }
    }
}
