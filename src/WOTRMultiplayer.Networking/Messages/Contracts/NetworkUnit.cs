using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkUnit
    {
        [ProtoMember(1)]
        [LogMe]
        public string Id { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public NetworkVector3 Position { get; set; }

        [ProtoMember(3)]
        public float Orientation { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public NetworkUnitTurnBasedInfo TurnBasedInfo { get; set; }

        [ProtoMember(5)]
        public NetworkUnitCombatState CombatState { get; set; }

        [ProtoMember(6)]
        public NetworkUnitDescriptor Descriptor { get; set; }

        [ProtoMember(7)]
        public NetworkUnitBuffCollection BuffCollection { get; set; }

        [ProtoMember(8)]
        public NetworkUnitPartInPit UnitPartInPit { get; set; }

        [ProtoMember(9)]
        public NetworkUnitPartKineticist UnitPartKineticist { get; set; }

        public override string ToString()
        {
            return Id?.ToString();
        }
    }
}
