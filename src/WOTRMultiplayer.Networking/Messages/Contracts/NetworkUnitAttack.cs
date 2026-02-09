using System.Collections.Generic;
using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkUnitAttack
    {
        [ProtoMember(1)]
        public string InitiatorUnitId { get; set; }

        [ProtoMember(2)]
        public string TargetUnitId { get; set; }

        [ProtoMember(3)]
        public bool IsFullAttack { get; set; }

        [ProtoMember(4)]
        public bool IsSingleAttack { get; set; }

        [ProtoMember(5)]
        public bool IsCharge { get; set; }

        [ProtoMember(6)]
        public List<NetworkVector3> VectorPath { get; set; }

        [ProtoMember(7)]
        public string MovementLimit { get; set; }
    }
}
