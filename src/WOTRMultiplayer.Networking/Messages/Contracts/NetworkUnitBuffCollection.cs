using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkUnitBuffCollection
    {
        [ProtoMember(1)]
        [LogMe]
        public List<NetworkBuff> Buffs { get; set; } = [];

        [ProtoMember(2)]
        [LogMe]
        public List<NetworkUnitNegativeLevelsData> NegativeLevels { get; set; } = [];
    }
}
