using System.Collections.Generic;
using NUnit.Framework;

namespace WOTRMultiplayer.Localization.UnitTests
{
    [TestFixture]
    public class WellKnownKeysTests
    {
        [TestCaseSource(nameof(GetWellKnownKeysTestCases))]
        public void Run_KeyPathIsInitializedCorrectly(WellKnownKeyTestCase testCase)
        {
            // Act
            WellKnownKeysInitializer.Run();
            var key = testCase.Key();

            // Assert
            Assert.That(key, Is.Not.Null.Or.Empty);
            Assert.That(key, Does.StartWith(WellKnownKeysInitializer.RootKey));
            Assert.That(key, Contains.Substring(WellKnownKeysInitializer.KeyPathSeparator), "Atleast one key path separator is expected");
        }

        /// <summary>
        /// no reason to fully cover each key as it's essentially the same method that does key path building
        /// </summary>
        /// <returns></returns>
        private static IEnumerable<WellKnownKeyTestCase> GetWellKnownKeysTestCases()
        {
            yield return new WellKnownKeyTestCase { Name = "settings->title", Key = () => WellKnownKeys.Settings.Title.Key };

            yield return new WellKnownKeyTestCase { Name = "settings->general->title", Key = () => WellKnownKeys.Settings.General.Title.Key };
            yield return new WellKnownKeyTestCase { Name = "settings->general->playerName->title", Key = () => WellKnownKeys.Settings.General.PlayerName.Title.Key };
            yield return new WellKnownKeyTestCase { Name = "settings->general->playerName->tooltip", Key = () => WellKnownKeys.Settings.General.PlayerName.Tooltip.Key };

            yield return new WellKnownKeyTestCase { Name = "settings->combat->title", Key = () => WellKnownKeys.Settings.Combat.Title.Key };
            yield return new WellKnownKeyTestCase { Name = "settings->combat->aiSync->title", Key = () => WellKnownKeys.Settings.Combat.SyncAI.Title.Key };
            yield return new WellKnownKeyTestCase { Name = "settings->combat->aiSync->tooltip", Key = () => WellKnownKeys.Settings.Combat.SyncAI.Tooltip.Key };

            yield return new WellKnownKeyTestCase { Name = "settings->networking->title", Key = () => WellKnownKeys.Settings.Networking.Title.Key };
            yield return new WellKnownKeyTestCase { Name = "settings->networking->hostPortStart->title", Key = () => WellKnownKeys.Settings.Networking.HostPortRangeStart.Title.Key };
            yield return new WellKnownKeyTestCase { Name = "settings->networking->hostPortStart->tooltip", Key = () => WellKnownKeys.Settings.Networking.HostPortRangeStart.Tooltip.Key };
            yield return new WellKnownKeyTestCase { Name = "settings->networking->hostPortEnd->title", Key = () => WellKnownKeys.Settings.Networking.HostPortRangeEnd.Title.Key };
            yield return new WellKnownKeyTestCase { Name = "settings->networking->hostPortEnd->tooltip", Key = () => WellKnownKeys.Settings.Networking.HostPortRangeEnd.Tooltip.Key };

            yield return new WellKnownKeyTestCase { Name = "settings->dangerZone->title", Key = () => WellKnownKeys.Settings.DangerZone.Title.Key };
            yield return new WellKnownKeyTestCase { Name = "settings->dangerZone->defaultForcedPause->title", Key = () => WellKnownKeys.Settings.DangerZone.DefaultForcedPauseTimeout.Title.Key };
            yield return new WellKnownKeyTestCase { Name = "settings->dangerZone->defaultForcedPause->tooltip", Key = () => WellKnownKeys.Settings.DangerZone.DefaultForcedPauseTimeout.Tooltip.Key };
            yield return new WellKnownKeyTestCase { Name = "settings->dangerZone->restEncounterForcedPause->title", Key = () => WellKnownKeys.Settings.DangerZone.RestEncounterForcedPauseTimeout.Title.Key };
            yield return new WellKnownKeyTestCase { Name = "settings->dangerZone->restEncounterForcedPause->tooltip", Key = () => WellKnownKeys.Settings.DangerZone.RestEncounterForcedPauseTimeout.Tooltip.Key };

            yield return new WellKnownKeyTestCase { Name = "mainMenu->multiplaye->title", Key = () => WellKnownKeys.MainMenu.Multiplayer.Title.Key };
            yield return new WellKnownKeyTestCase { Name = "escMenu->multiplayerLobby->title", Key = () => WellKnownKeys.EscMenu.MultiplayerLobby.Title.Key };

            yield return new WellKnownKeyTestCase { Name = "multiplayerWindow->hostMenu->title", Key = () => WellKnownKeys.MultiplayerWindow.HostMenu.Title.Key };
            yield return new WellKnownKeyTestCase { Name = "multiplayerWindow->hostMenu->hostButton->hostText", Key = () => WellKnownKeys.MultiplayerWindow.HostMenu.HostButton.HostText.Key };
            yield return new WellKnownKeyTestCase { Name = "multiplayerWindow->hostMenu->hostButton->selectSaveText", Key = () => WellKnownKeys.MultiplayerWindow.HostMenu.HostButton.SelectSaveText.Key };
            yield return new WellKnownKeyTestCase { Name = "multiplayerWindow->hostMenu->readyButton->readyText", Key = () => WellKnownKeys.MultiplayerWindow.HostMenu.ReadyButton.ReadyText.Key };
            yield return new WellKnownKeyTestCase { Name = "multiplayerWindow->hostMenu->readyButton->notReadyText", Key = () => WellKnownKeys.MultiplayerWindow.HostMenu.ReadyButton.NotReadyText.Key };
            yield return new WellKnownKeyTestCase { Name = "multiplayerWindow->hostMenu->startButton->title", Key = () => WellKnownKeys.MultiplayerWindow.HostMenu.StartButton.Key };
            yield return new WellKnownKeyTestCase { Name = "multiplayerWindow->joinMenu->deactivation->hosting", Key = () => WellKnownKeys.MultiplayerWindow.HostMenu.Deactivation.Hosting.Key };

            yield return new WellKnownKeyTestCase { Name = "multiplayerWindow->joinMenu->title", Key = () => WellKnownKeys.MultiplayerWindow.JoinMenu.Title.Key };
            yield return new WellKnownKeyTestCase { Name = "multiplayerWindow->joinMenu->hostButton->title", Key = () => WellKnownKeys.MultiplayerWindow.JoinMenu.JoinButton.Key };
            yield return new WellKnownKeyTestCase { Name = "multiplayerWindow->joinMenu->readyButton->readyText", Key = () => WellKnownKeys.MultiplayerWindow.JoinMenu.ReadyButton.ReadyText.Key };
            yield return new WellKnownKeyTestCase { Name = "multiplayerWindow->joinMenu->readyButton->notReadyText", Key = () => WellKnownKeys.MultiplayerWindow.JoinMenu.ReadyButton.NotReadyText.Key };
            yield return new WellKnownKeyTestCase { Name = "multiplayerWindow->joinMenu->leaveButton->title", Key = () => WellKnownKeys.MultiplayerWindow.JoinMenu.LeaveButton.Key };
            yield return new WellKnownKeyTestCase { Name = "multiplayerWindow->joinMenu->serverAddress->placeholder", Key = () => WellKnownKeys.MultiplayerWindow.JoinMenu.ServerAddress.Placeholder.Key };
            yield return new WellKnownKeyTestCase { Name = "multiplayerWindow->joinMenu->deactivation->connecting", Key = () => WellKnownKeys.MultiplayerWindow.JoinMenu.Deactivation.Connecting.Key };
            yield return new WellKnownKeyTestCase { Name = "multiplayerWindow->joinMenu->deactivation->connected", Key = () => WellKnownKeys.MultiplayerWindow.JoinMenu.Deactivation.Connected.Key };

            yield return new WellKnownKeyTestCase { Name = "lobbyWindow->server->title", Key = () => WellKnownKeys.LobbyWindow.Server.Title.Key };
            yield return new WellKnownKeyTestCase { Name = "lobbyWindow->players->title", Key = () => WellKnownKeys.LobbyWindow.Players.Title.Key };
            yield return new WellKnownKeyTestCase { Name = "lobbyWindow->characters->title", Key = () => WellKnownKeys.LobbyWindow.Characters.Title.Key };

            yield return new WellKnownKeyTestCase { Name = "gameNotifications->rolls->failedToAcquireRemoteDamageRoll", Key = () => WellKnownKeys.GameNotifications.Rolls.FailedToAcquireRemoteDamageRoll.Key };
        }
    }
}
