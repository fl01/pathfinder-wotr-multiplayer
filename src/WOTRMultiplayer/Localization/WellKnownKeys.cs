using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace WOTRMultiplayer.Localization
{
    [Description(RootKey)]
    public static class WellKnownKeys
    {
        public const string KeyPathSeparator = ".";
        public const string RootKey = "wotrmultiplayer";

        public static void Initialize()
        {
            var typesToProcess = new Stack<(string, Type)>();
            var rootType = typeof(WellKnownKeys);
            var rootPath = rootType.GetCustomAttribute<DescriptionAttribute>().Description;
            typesToProcess.Push((rootPath, rootType));
            while (typesToProcess.Count > 0)
            {
                var (currentPath, currentType) = typesToProcess.Pop();
                var children = currentType.GetNestedTypes().Where(n => n.IsClass && n.GetCustomAttribute<DescriptionAttribute>() != null).ToList();
                if (children.Count > 0)
                {
                    foreach (var child in children)
                    {
                        var childPath = string.Join(KeyPathSeparator, currentPath, child.GetCustomAttribute<DescriptionAttribute>().Description);
                        typesToProcess.Push((childPath, child));
                    }

                    continue;
                }

                var keyProperty = currentType.GetProperty("Key")
                    ?? throw new InvalidOperationException($"A well-known key type has neither children nor Key property. Type={currentType}");

                keyProperty.SetValue(null, currentPath);
            }
        }

        [Description("mainMenu")]
        public static class MainMenu
        {
            [Description("multiplayer")]
            public static class Multiplayer
            {
                [Description("title")]
                public static class Title
                {
                    public static string Key { get; set; }
                }
            }
        }

        [Description("escMenu")]
        public static class EscMenu
        {
            [Description("multiplayerLobby")]
            public static class MultiplayerLobby
            {
                [Description("title")]
                public static class Title
                {
                    public static string Key { get; set; }
                }
            }
        }

        [Description("multiplaterWindow")]
        public static class MultiplayerWindow
        {
            [Description("hostMenu")]
            public static class HostMenu
            {
                [Description("title")]
                public static class Title
                {
                    public static string Key { get; set; }
                }

                [Description("hostButton")]
                public static class HostButton
                {

                    [Description("hostText")]
                    public static class HostText
                    {
                        public static string Key { get; set; }
                    }

                    [Description("selectSaveText")]
                    public static class SelectSaveText
                    {
                        public static string Key { get; set; }
                    }
                }

                [Description("readyButton")]
                public static class ReadyButton
                {
                    [Description("readyText")]
                    public static class ReadyText
                    {
                        public static string Key { get; set; }
                    }

                    [Description("notReadyText")]
                    public static class NotReadyText
                    {
                        public static string Key { get; set; }
                    }
                }

                [Description("startButton")]
                public static class StartButton
                {
                    public static string Key { get; set; }
                }

                [Description("deactivation")]
                public static class Deactivation
                {
                    [Description("hosting")]
                    public static class Hosting
                    {
                        public static string Key { get; set; }
                    }
                }
            }

            [Description("joinMenu")]
            public static class JoinMenu
            {
                [Description("title")]
                public static class Title
                {
                    public static string Key { get; set; }
                }

                [Description("joinButton")]
                public static class JoinButton
                {
                    public static string Key { get; set; }
                }

                [Description("readyButton")]
                public static class ReadyButton
                {
                    [Description("readyText")]
                    public static class ReadyText
                    {
                        public static string Key { get; set; }
                    }

                    [Description("notReadyText")]
                    public static class NotReadyText
                    {
                        public static string Key { get; set; }
                    }
                }

                [Description("leaveButton")]
                public static class LeaveButton
                {
                    public static string Key { get; set; }
                }

                [Description("serverAddress")]
                public static class ServerAddress
                {
                    [Description("placeholder")]
                    public static class Placeholder
                    {
                        public static string Key { get; set; }
                    }
                }

                [Description("deactivation")]
                public static class Deactivation
                {
                    [Description("connected")]
                    public static class Connected
                    {
                        public static string Key { get; set; }
                    }

                    [Description("connecting")]
                    public static class Connecting
                    {
                        public static string Key { get; set; }
                    }
                }
            }
        }

        [Description("lobbyWindow")]
        public static class LobbyWindow
        {
            [Description("server")]
            public static class Server
            {
                [Description("title")]
                public static class Title
                {
                    public static string Key { get; set; }
                }
            }

            [Description("players")]
            public static class Players
            {
                [Description("title")]
                public static class Title
                {
                    public static string Key { get; set; }
                }
            }

            [Description("characters")]
            public static class Characters
            {
                [Description("title")]
                public static class Title
                {
                    public static string Key { get; set; }
                }
            }
        }

        [Description("settings")]
        public static class Settings
        {
            [Description("title")]
            public static class Title
            {
                public static string Key { get; set; }
            }

            [Description("general")]
            public static class General
            {
                [Description("title")]
                public static class Title
                {
                    public static string Key { get; set; }
                }

                [Description("playerName")]
                public static class PlayerName
                {
                    [Description("title")]
                    public static class Title
                    {
                        public static string Key { get; set; }
                    }

                    [Description("tooltip")]
                    public static class Tooltip
                    {
                        public static string Key { get; set; }
                    }
                }
            }

            [Description("combat")]
            public static class Combat
            {
                [Description("title")]
                public static class Title
                {
                    public static string Key { get; set; }
                }

                [Description("aiSync")]
                public static class AISync
                {
                    [Description("title")]
                    public static class Title
                    {
                        public static string Key { get; set; }
                    }

                    [Description("tooltip")]
                    public static class Tooltip
                    {
                        public static string Key { get; set; }
                    }
                }
            }

            [Description("networking")]
            public static class Networking
            {
                [Description("title")]
                public static class Title
                {
                    public static string Key { get; set; }
                }

                [Description("hostPortStart")]
                public static class HostPortRangeStart
                {
                    [Description("title")]
                    public static class Title
                    {
                        public static string Key { get; set; }
                    }

                    [Description("tooltip")]
                    public static class Tooltip
                    {
                        public static string Key { get; set; }
                    }
                }

                [Description("hostPortEnd")]
                public static class HostPortRangeEnd
                {
                    [Description("title")]
                    public static class Title
                    {
                        public static string Key { get; set; }
                    }

                    [Description("tooltip")]
                    public static class Tooltip
                    {
                        public static string Key { get; set; }
                    }
                }
            }

            [Description("dangerZone")]
            public static class DangerZone
            {
                [Description("title")]
                public static class Title
                {
                    public static string Key { get; set; }
                }

                [Description("defaultForcedPauseTimeout")]
                public static class DefaultForcedPauseTimeout
                {
                    [Description("title")]
                    public static class Title
                    {
                        public static string Key { get; set; }
                    }

                    [Description("tooltip")]
                    public static class Tooltip
                    {
                        public static string Key { get; set; }
                    }
                }

                [Description("restEncounterForcedPauseTimeout")]
                public static class RestEncounterForcedPauseTimeout
                {
                    [Description("title")]
                    public static class Title
                    {
                        public static string Key { get; set; }
                    }

                    [Description("tooltip")]
                    public static class Tooltip
                    {
                        public static string Key { get; set; }
                    }
                }
            }
        }

        [Description("multiplayerClient")]
        public static class MultiplayerClient
        {
            [Description("errors")]
            public static class Errors
            {
                [Description("invalidAddress")]
                public static class InvalidAddress
                {
                    public static string Key { get; set; }
                }

                [Description("invalidPort")]
                public static class InvalidPort
                {
                    public static string Key { get; set; }
                }

                [Description("disconnected")]
                public static class Disconnected
                {
                    public static string Key { get; set; }
                }

                [Description("genericError")]
                public static class GenericError
                {
                    public static string Key { get; set; }
                }

                [Description("networkError")]
                public static class NetworkError
                {
                    public static string Key { get; set; }
                }
            }
        }

        [Description("gameNotifications")]
        public static class GameNotifications
        {
            [Description("rolls")]
            public static class Rolls
            {
                [Description("failedToAcquireRemoteDamageRoll")]
                public static class FailedToAcquireRemoteDamageRoll
                {
                    public static string Key { get; set; }
                }

                [Description("failedToAcquireRemoteHealRoll")]
                public static class FailedToAcquireRemoteHealRoll
                {
                    public static string Key { get; set; }
                }

                [Description("failedToAcquireRemoteRoll")]
                public static class FailedToAcquireRemoteRoll
                {
                    public static string Key { get; set; }
                }

                [Description("invalidRemoteDamageRoll")]
                public static class InvalidRemoteDamageRoll
                {
                    public static string Key { get; set; }
                }
            }

            [Description("rest")]
            public static class Rest
            {
                [Description("noCampingPermission")]
                public static class NoCampingPermission
                {
                    public static string Key { get; set; }
                }
            }

            [Description("leveling")]
            public static class Leveling
            {
                [Description("completed")]
                public static class Completed
                {
                    public static string Key { get; set; }
                }

                [Description("terminated")]
                public static class Terminated
                {
                    public static string Key { get; set; }
                }

                [Description("ArchetypeContentMismatch")]
                public static class ArchetypeContentMismatch
                {
                    public static string Key { get; set; }
                }
            }

            [Description("session")]
            public static class Session
            {
                [Description("playerJoined")]
                public static class PlayerJoined
                {
                    public static string Key { get; set; }
                }

                [Description("playerLeft")]
                public static class PlayerLeft
                {
                    public static string Key { get; set; }
                }
            }

            [Description("spellBook")]
            public static class SpellBook
            {
                [Description("noSpellSlotPermission")]
                public static class NoSpellSlotPermission
                {
                    public static string Key { get; set; }
                }

                [Description("memorizedSpell")]
                public static class MemorizedSpell
                {
                    public static string Key { get; set; }
                }

                [Description("forgottenSpell")]
                public static class ForgottenSpell
                {
                    public static string Key { get; set; }
                }
            }

            [Description("forcedPause")]
            public static class ForcedPause
            {
                [Description("noPermission")]
                public static class NoPermission
                {
                    public static string Key { get; set; }
                }

                [Description("areaLoading")]
                public static class AreaLoading
                {
                    public static string Key { get; set; }
                }

                [Description("restRandomEncounterLoading")]
                public static class RestRandomEncounterLoading
                {
                    public static string Key { get; set; }
                }

                [Description("notSyncedPauseYet")]
                public static class NotSyncedPauseYet
                {
                    public static string Key { get; set; }
                }

                [Description("noTrapDetectedYet")]
                public static class NoTrapDetectedYet
                {
                    public static string Key { get; set; }
                }
            }

            [Description("combat")]
            public static class Combat
            {
                [Description("clientTurnOrderDesync")]
                public static class ClientTurnOrderDesync
                {
                    public static string Key { get; set; }
                }

                [Description("hostTurnOrderDesync")]
                public static class HostTurnOrderDesync
                {
                    public static string Key { get; set; }
                }
            }
        }
    }
}
