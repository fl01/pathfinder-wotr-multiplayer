using System.Collections.Generic;
using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkContentState
    {
        [ProtoMember(1)]
        public List<NetworkDLC> DLCs { get; set; } = [];

        [ProtoMember(2)]
        public List<NetworkMod> Mods { get; set; } = [];

        [ProtoMember(3)]
        public List<NetworkDiscrepantDLC> DiscrepantDLCs { get; set; } = [];

        [ProtoMember(4)]
        public List<NetworkDiscrepantMod> DiscrepantMods { get; set; } = [];

        [ProtoMember(5)]
        public string GameVersion { get; set; }
    }
}
