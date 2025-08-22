using Serilog.Events;
using UnityModManagerNet;

namespace WOTRMultiplayer.Config.UnityMod
{
    public class UnityModManagerSettings : UnityModManager.ModSettings
    {
        public bool UseDebugConsole { get; set; } = false;

        public int MinimumLogLevel { get; set; } = (int)LogEventLevel.Information;

        public bool AddUnitIdToOvertip { get; set; } = false;
    }
}
