using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkCombatUnitDiscrepancy
    {
        [ProtoMember(1)]
        [LogMe]
        public Dictionary<long, List<NetworkUnit>> Units { get; set; } = [];
    }
}
