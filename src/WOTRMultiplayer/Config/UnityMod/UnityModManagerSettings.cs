using UnityModManagerNet;

namespace WOTRMultiplayer.Config.UnityMod
{
    public class UnityModManagerSettings : UnityModManager.ModSettings
    {
        public bool UseDebugConsole { get; set; }

        public string MinimumLogLevel { get; set; }

        public bool AddUnitIdToOvertip { get; set; }
    }
}
