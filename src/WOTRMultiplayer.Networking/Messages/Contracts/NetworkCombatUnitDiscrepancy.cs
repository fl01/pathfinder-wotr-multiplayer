using System.Collections.Generic;
using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkCombatUnitDiscrepancy
    {
        [ProtoMember(1)]
        public Dictionary<long, List<NetworkUnit>> Units { get; set; } = [];
    }
}
