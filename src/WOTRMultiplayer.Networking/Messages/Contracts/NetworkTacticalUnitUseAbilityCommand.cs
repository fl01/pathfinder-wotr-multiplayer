using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkTacticalUnitUseAbilityCommand
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkAbility Ability { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string InitiatorUnitId { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public NetworkTargetWrapper Target { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public List<NetworkVector3> VectorPath { get; set; }
    }
}
