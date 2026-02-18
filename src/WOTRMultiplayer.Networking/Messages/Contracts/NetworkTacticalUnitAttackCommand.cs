using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkTacticalUnitAttackCommand
    {
        [ProtoMember(1)]
        [LogMe]
        public string UnitId { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public List<NetworkVector3> Path { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public string TargetUnitId { get; set; }
    }
}
