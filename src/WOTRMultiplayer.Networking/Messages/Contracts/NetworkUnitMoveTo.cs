using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkUnitMoveTo
    {
        [ProtoMember(1)]
        [LogMe]
        public string InitiatorUnitId { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public List<NetworkVector3> VectorPath { get; set; } = [];

        [ProtoMember(3)]
        [LogMe]
        public NetworkVector3 Destination { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public string MovementLimit { get; set; }

        [ProtoMember(5)]
        [LogMe]
        public float? Orientation { get; set; }

        [ProtoMember(6)]
        [LogMe]
        public float MovementDelay { get; set; }

        [ProtoMember(7)]
        [LogMe]
        public string AttackMode { get; set; }

        [ProtoMember(8)]
        [LogMe]
        public float? SpeedLimit { get; set; }

        [ProtoMember(9)]
        [LogMe]
        public bool ApplySpeedLimitInCombat { get; set; }
    }
}
