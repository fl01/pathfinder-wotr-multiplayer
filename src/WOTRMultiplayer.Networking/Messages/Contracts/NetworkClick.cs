using System.Collections.Generic;
using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkClick
    {
        [ProtoMember(1)]
        public NetworkVector3 WorldPosition { get; set; }

        [ProtoMember(2)]
        public string TargetUnitId { get; set; }

        [ProtoMember(3)]
        public string MapObjectId { get; set; }

        [ProtoMember(4)]
        public bool IsLootBagMapObject { get; set; }

        [ProtoMember(5)]
        public int Button { get; set; }

        [ProtoMember(6)]
        public bool MuteEvents { get; set; }

        [ProtoMember(7)]
        public List<string> SelectedUnits { get; set; } = [];

        [ProtoMember(8)]
        public List<NetworkVector3> VectorPath { get; set; } = [];

        [ProtoMember(9)]
        public bool IsTMBClick { get; set; }

        [ProtoMember(10)]
        public string MovementLimit { get; set; }
    }
}
