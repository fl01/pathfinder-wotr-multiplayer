using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.Content
{
    public class NetworkContentState
    {
        public string GameVersion { get; set; }

        public List<NetworkDLC> DLCs { get; set; } = [];

        public List<NetworkMod> Mods { get; set; } = [];

        public List<NetworkDiscrepantDLC> DiscrepantDLCs { get; set; } = [];

        public List<NetworkDiscrepantMod> DiscrepantMods { get; set; } = [];
    }
}
