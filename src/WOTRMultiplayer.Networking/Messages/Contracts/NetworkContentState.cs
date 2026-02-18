using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkContentState
    {
        [ProtoMember(1)]
        [LogMe]
        public List<NetworkDLC> DLCs { get; set; } = [];

        [ProtoMember(2)]
        [LogMe]
        public List<NetworkMod> Mods { get; set; } = [];

        [ProtoMember(3)]
        [LogMe]
        public List<NetworkDiscrepantDLC> DiscrepantDLCs { get; set; } = [];

        [ProtoMember(4)]
        [LogMe]
        public List<NetworkDiscrepantMod> DiscrepantMods { get; set; } = [];

        [ProtoMember(5)]
        [LogMe]
        public string GameVersion { get; set; }
    }
}
