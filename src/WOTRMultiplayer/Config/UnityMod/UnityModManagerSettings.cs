using UnityModManagerNet;

namespace WOTRMultiplayer.Config.UnityMod
{
    public class UnityModManagerSettings : UnityModManager.ModSettings
    {
        public bool UseDebugConsole { get; set; }

        public int MinimumLogLevel { get; set; }

        public bool AddUnitIdToOvertip { get; set; }
    }
}
