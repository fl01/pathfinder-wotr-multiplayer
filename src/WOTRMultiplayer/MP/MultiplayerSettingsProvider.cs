using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.MP;

namespace WOTRMultiplayer.MP
{
    public class MultiplayerSettingsProvider : IMultiplayerSettingsProvider
    {
        private readonly ILogger<MultiplayerSettingsProvider> _logger;

        private MultiplayerSettings _settings;
        public MultiplayerSettings Settings => _settings ??= InitiDefault();

        public MultiplayerSettingsProvider(ILogger<MultiplayerSettingsProvider> logger)
        {
            _logger = logger;
        }

        private MultiplayerSettings InitiDefault()
        {
            return new MultiplayerSettings
            {
                PlayerName = Guid.NewGuid().ToString().Split('-').First(),
                HostPortRangeStart = 1024,
                HostPortRangeEnd = ushort.MaxValue,
                ForcedPauseDefaultTerminationDelay = TimeSpan.FromSeconds(3),
                ForcedPauseRandomEncounterTerminationDelay = TimeSpan.FromSeconds(4),
            };
        }
    }
}
