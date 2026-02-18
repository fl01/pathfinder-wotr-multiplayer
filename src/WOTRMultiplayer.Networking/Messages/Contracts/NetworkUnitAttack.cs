using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkUnitAttack
    {
        [ProtoMember(1)]
        [LogMe]
        public string InitiatorUnitId { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string TargetUnitId { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public bool IsFullAttack { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public bool IsSingleAttack { get; set; }

        [ProtoMember(5)]
        [LogMe]
        public bool IsCharge { get; set; }

        [ProtoMember(6)]
        [LogMe]
        public List<NetworkVector3> VectorPath { get; set; }

        [ProtoMember(7)]
        [LogMe]
        public string MovementLimit { get; set; }
    }
}
