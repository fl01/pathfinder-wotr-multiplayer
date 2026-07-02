using Serilog.Events;
using UnityModManagerNet;

namespace WOTRMultiplayer.Services.Settings
{
    public class UnityModManagerSettings : UnityModManager.ModSettings
    {
        public string ModId { get; set; }

        public string ModFolder { get; set; }

        public bool UseDebugConsole { get; set; } = false;

        public int GlobalMinimumLogLevel { get; set; } = (int)LogEventLevel.Information;

        public int ConsoleMinimumLogLevel { get; set; } = (int)LogEventLevel.Information;

        public int FileMinimumLogLevel { get; set; } = (int)LogEventLevel.Information;

        public bool AddUnitIdToOvertip { get; set; } = false;
    }
}
