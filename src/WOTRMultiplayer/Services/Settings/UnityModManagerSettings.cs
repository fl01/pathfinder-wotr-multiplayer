using Serilog.Events;
using UnityModManagerNet;

namespace WOTRMultiplayer.Services.Settings
{
    public class UnityModManagerSettings : UnityModManager.ModSettings
    {
        public bool UseDebugConsole { get; set; } = false;

        public int ConsoleMinimumLogLevel { get; set; } = (int)LogEventLevel.Information;

        public int FileMinimumLogLevel { get; set; } = (int)LogEventLevel.Debug;

        public bool AddUnitIdToOvertip { get; set; } = false;
    }
}
