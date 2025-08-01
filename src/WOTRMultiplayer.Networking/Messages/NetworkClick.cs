using System.Collections.Generic;
using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages
{
    [ProtoContract]
    public class NetworkClick
    {
        [ProtoMember(1)]
        public string TargetUnitId { get; set; }

        [ProtoMember(2)]
        public string MapObjectId { get; set; }

        [ProtoMember(3)]
        public int Button { get; set; }

        [ProtoMember(4)]
        public bool MuteEvents { get; set; }

        [ProtoMember(5)]
        public List<string> SelectedUnits { get; set; } = [];

        [ProtoMember(6)]
        public NetworkVector3 WorldPosition { get; set; }

        [ProtoMember(7)]
        public List<NetworkVector3> VectorPath { get; set; } = [];

        [ProtoMember(8)]
        public NetworkActionsState ActionsState { get; set; }

        [ProtoMember(9)]
        public bool IsTurnBasedModeClick { get; set; }
    }
}
