using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkPing
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkVector3 WorldPosition { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string UnitId { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public NetworkMapObject MapObject { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public NetworkGlobalMapLocation GlobalMapLocation { get; set; }

        [ProtoMember(5)]
        [LogMe]
        public NetworkGlobalMapArmy GlobalMapArmy { get; set; }

        [ProtoMember(6)]
        [LogMe]
        public NetworkGlobalMapKingdomSettlement GlobalMapKingdomSettlement { get; set; }

        [ProtoMember(7)]
        [LogMe]
        public string Type { get; set; }
    }
}
