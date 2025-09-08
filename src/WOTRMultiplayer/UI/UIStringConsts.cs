namespace WOTRMultiplayer.UI
{
    /// <summary>
    /// should be replaced with localization later on
    /// </summary>
    public class UIStringConsts
    {
        public class MainMenu
        {
            public const string MultiplayerMenu = "Multiplayer";
        }

        public class EscMenu
        {
            public const string LobbyMenuItemTitle = "Multiplayer Lobby";
        }

        public class MultiplayerClient
        {
            public class Errors
            {
                public const string InvalidIP = "Unable to parse provided server address.\nPlease verify your input.";
                public const string InvalidPort = "Invalid port specified.\nAcceptable range is '0 > port <= 65535'.";
            }
        }

        public class MultiplayerWindow
        {
            public const string HostMenuLabel = "Host";
            public const string JoinMenuLabel = "Join";

            public class JoinMenu
            {
                public const string LeaveGameMessage = "You are currently in a game. Proceeding with this action will result in leaving it";

                public const string JoinButtonLabel = "Join";
                public const string ReadyButtonLabel = "Ready";
                public const string ReadyNotReadyButtonLabel = "Not Ready";
                public const string LeaveButtonLabel = "Leave";
                public const string ServerInputPlaceholder = "Enter ip:port";

                public const string LeaveWhileConnectingMessage = "Can't leave while connecting.\nPlease wait";
            }

            public class HostMenu
            {
                public const string TerminateServerMessage = "You are currently hosting a game. Proceeding with this action will result in its termination.";

                public const string HostButtonLabel = "Host";
                public const string HostButtonActiveLabel = "Select save";
                public const string ReadyButtonLabel = "Ready";
                public const string ReadyNotReadyButtonLabel = "Not Ready";
                public const string StartButtonLabel = "Start";
            }
        }

        public class LobbyInfoWindow
        {
            public const string ServerInfoSectionTitle = "Server";
            public const string PlayersSectionTitle = "Players";
            public const string CharactersSectionTitle = "Characters";
        }

        public class GameNotifications
        {
            public const string TryingToSetUpCampAsAClient = "Camp can be placed by the host only";

            public const string CantChangeSpellSlotsIfNoControl = "Changing spells is restricted to character owner only";

            public const string MismatchedArchetypeSelection = "Unable to selected archetype due to mismatched game content (DLCs/mods)";

            public const string LevelingCompleted = "{0}'s leveling has been completed";
            public const string LevelingTerminated = "{0}'s leveling has been terminated";

            public const string PlayerJoined = "Player {0} has joined the game";
            public const string PlayerLeft = "Player {0} has left the game";

            public const string FailedToAcquireRemoteDamageRoll = "Failed to acquire damage roll from remote player which guarantees desync in the game";
            public const string FailedToAcquireRemoteHealRoll = "Failed to acquire heal roll from remote player which guarantees desync in the game";
            public const string InvalidRemoteDamageRoll = "Network damage contains an invalid number of damage values which guarantees desync in the game";
            public const string FailedToAcquireRemoteRoll = "Failed to acquire {0} roll from remote player which guarantees desync in the game.";

            public const string TryingToUnpauseAsAClient = "Pause can be removed by host only";

            public class ForcedPauseReasons
            {
                public const string AreaLoading = "Please wait for other players to finish loading the area";
                public const string RandomEncounterLoading = "Please wait for other players to finish loading the random encounter";
                public const string WaitingForPlayersToPause = "Can't unpause until everyone is paused";
                public const string TrapDetected = "Waiting for everyone to detect trap";
            }

            public class CombatLog
            {
                public const string ClientIsFixingCombaTurnOrderDesync = "Host detected desync in turn order, fixing...";
                public const string HostDetectedDesyncInCombatTurnOrder = "Player {0} is trying to start different turn, fixing...";

                public const string SpellMemorized = "{0} has been memorized by {1}";
                public const string SpellForgotten = "{0} has been forgotten by {1}";
            }
        }
    }
}
