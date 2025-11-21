using System.Collections.Generic;
using NUnit.Framework;
using WOTRMultiplayer.Settings;

namespace WOTRMultiplayer.UnitTests.Settings
{
    [TestFixture]
    public class WellKnownSettingsTests
    {
        [TestCaseSource(nameof(GetWellKnownKeysTestCases))]
        public void Initialize_KeyPathIsInitializedCorrectly(WellKnownSettingTestCase testCase)
        {
            // Act
            WellKnownSettings.Initialize();
            var key = testCase.Key();

            // Assert
            Assert.That(key, Is.Not.Null.Or.Empty);
            Assert.That(key, Does.StartWith(WellKnownSettings.RootKey));
            Assert.That(key, Contains.Substring(WellKnownSettings.KeyPathSeparator), "Atleast one key path separator is expected");
        }

        private static IEnumerable<WellKnownSettingTestCase> GetWellKnownKeysTestCases()
        {
            yield return new WellKnownSettingTestCase { Name = "general->playerName", Key = () => WellKnownSettings.General.PlayerName.Key };

            yield return new WellKnownSettingTestCase { Name = "combat->aiSync", Key = () => WellKnownSettings.Combat.SyncAI.Key };

            yield return new WellKnownSettingTestCase { Name = "networking->hostPortRangeStart", Key = () => WellKnownSettings.Networking.HostPortRangeStart.Key };
            yield return new WellKnownSettingTestCase { Name = "networking->hostPortRangeEnd", Key = () => WellKnownSettings.Networking.HostPortRangeEnd.Key };

            yield return new WellKnownSettingTestCase { Name = "dangerZone->defaultForcedPauseTimeout", Key = () => WellKnownSettings.DangerZone.DefaultForcedPauseTimeout.Key };
            yield return new WellKnownSettingTestCase { Name = "dangerZone->restEncounterForcedPauseTimeout", Key = () => WellKnownSettings.DangerZone.RestEncounterForcedPauseTimeout.Key };
        }
    }
}
