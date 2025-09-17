using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkUnitTurnBasedInfo
    {
        [ProtoMember(1)]
        public bool Surprising { get; set; }

        [ProtoMember(2)]
        public bool Surprised { get; set; }

        [ProtoMember(3)]
        public bool ActingInSurpriseRound { get; set; }
    }
}
