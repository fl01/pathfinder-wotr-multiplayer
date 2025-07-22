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
    }
}
