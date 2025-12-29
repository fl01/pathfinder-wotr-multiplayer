using System.Collections.Generic;
using WOTRMultiplayer.Entities.Content;

namespace WOTRMultiplayer.UnitTests.Services
{
    public class ContentStateTestCase
    {
        public string Description { get; set; }

        public List<NetworkDLC> HostDLCs { get; set; } = [];

        public List<Networking.Messages.Contracts.NetworkDLC> PlayerDLCs { get; set; } = [];

        public List<NetworkDiscrepantDLC> ExpectedDiscrepantDLCs { get; set; } = [];

        public List<NetworkMod> HostMods { get; set; } = [];

        public List<Networking.Messages.Contracts.NetworkMod> PlayerMods { get; set; } = [];

        public List<NetworkDiscrepantMod> ExpectedDiscrepantMods { get; set; } = [];

        public ContentStateTestCase(string description)
        {
            Description = description;
        }

        public override string ToString()
        {
            return Description;
        }
    }
}
