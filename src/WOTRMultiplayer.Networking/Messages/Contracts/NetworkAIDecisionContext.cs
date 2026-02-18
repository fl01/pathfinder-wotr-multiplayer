using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkAIDecisionContext
    {
        [ProtoMember(1)]
        [LogMe]
        public List<NetworkVector3> VectorPath { get; set; } = [];

        [ProtoMember(2)]
        [LogMe]
        public bool BestEnableFiveFootStep { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public NetworkVector3 BestDestinationPoint { get; set; }
    }
}
