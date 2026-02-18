using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkClick
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkVector3 WorldPosition { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string TargetUnitId { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public string MapObjectId { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public bool IsLootBagMapObject { get; set; }

        [ProtoMember(5)]
        public int Button { get; set; }

        [ProtoMember(6)]
        public bool MuteEvents { get; set; }

        [ProtoMember(7)]
        [LogMe]
        public List<string> SelectedUnits { get; set; } = [];

        [ProtoMember(8)]
        [LogMe]
        public List<NetworkVector3> VectorPath { get; set; } = [];

        [ProtoMember(9)]
        public bool IsTMBClick { get; set; }

        [ProtoMember(10)]
        [LogMe]
        public string MovementLimit { get; set; }
    }
}
