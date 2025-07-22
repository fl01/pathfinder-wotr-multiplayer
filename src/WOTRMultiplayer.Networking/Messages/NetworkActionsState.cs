using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages
{
    [ProtoContract]
    public class NetworkActionsState
    {
        [ProtoMember(1)]
        public NetworkVector3 ApproachPoint { get; set; }

        [ProtoMember(2)]
        public float ApproachRadius { get; set; }

        [ProtoMember(3)]
        public NetworkCombatAction Move { get; set; }

        [ProtoMember(4)]
        public NetworkCombatAction Swift { get; set; }

        [ProtoMember(5)]
        public NetworkCombatAction Standard { get; set; }

        [ProtoMember(6)]
        public NetworkCombatAction FiveFootStep { get; set; }

        [ProtoMember(7)]
        public NetworkCombatAction Free { get; set; }
    }
}
