using System.Collections.Concurrent;
using System.Collections.Generic;
using Kingmaker.GameModes;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.Combat.Crusades;
using WOTRMultiplayer.Entities.Dialogs;
using WOTRMultiplayer.Entities.GlobalMap;
using WOTRMultiplayer.Entities.Leveling;

namespace WOTRMultiplayer.Entities
{
    public class NetworkGame
    {
        public string Id { get; set; }

        public int SessionSeed { get; set; }

        public long LocalPlayerId { get; set; }

        public NetworkGameStartUp StartUp { get; set; }

        public NetworkGameConnectivity Connectivity { get; set; }

        public NetworkLobbyStage Stage { get; set; }

        public List<NetworkPlayer> Players { get; set; } = [];

        public Dictionary<string, long> CharactersOwnershipHistory { get; set; } = [];

        public List<NetworkCharacter> Characters { get; set; } = [];

        public NetworkArmyCombat ArmyCombat { get; set; }

        public NetworkCombat Combat { get; set; }

        public NetworkCombatTurn LastCombatTurn { get; set; }

        public NetworkDialog Dialog { get; set; }

        public NetworkForcedPause ForcedPause { get; set; }

        public NetworkRest Rest { get; set; }

        public NetworkLeveling Leveling { get; set; }

        public ConcurrentDictionary<long, NetworkGlobalMapTravelerMode> PlayersInGlobalMapMode { get; set; } = [];

        public ConcurrentDictionary<GameModeType, HashSet<long>> PlayersInGameMode { get; set; } = [];

        public HashSet<long> PlayersInGroupChanger { get; set; } = [];

        public HashSet<long> PlayersInSkipTime { get; set; } = [];

        public HashSet<long> PlayersInGlobalMapLocationMessage { get; set; } = [];

        public HashSet<long> PlayersInGlobalMapCommonPopup { get; set; } = [];

        public HashSet<long> PlayersInGlobalMapCrusadeArmyBattleResults { get; set; } = [];

        public HashSet<long> PlayersInGlobalMapCombatResults { get; set; } = [];

        public HashSet<long> PlayersInGlobalMapCrusadeArmyInfo { get; set; } = [];

        public HashSet<long> PlayersInGlobalMapCrusadeArmyInfoMerge { get; set; } = [];

        public HashSet<long> PlayersInGlobalMapEncounterMessage { get; set; } = [];

        public HashSet<long> PlayersInZoneLoot { get; set; } = [];

        public HashSet<long> PlayersInDialogPopup { get; set; } = [];

        public HashSet<long> PlayersInCharacterSelectionWindow { get; set; } = [];

        public HashSet<long> PlayersInRespecWindow { get; set; } = [];

        public NetworkGame(NetworkGameStartUp gameStartup)
        {
            StartUp = gameStartup;
            Stage = NetworkLobbyStage.Lobby;
        }
    }
}
