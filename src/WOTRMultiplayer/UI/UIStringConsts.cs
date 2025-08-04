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
            public const string TryingToLeaveAsAClient = "Can't leave area as a multiplayer client";
        }
    }
}
