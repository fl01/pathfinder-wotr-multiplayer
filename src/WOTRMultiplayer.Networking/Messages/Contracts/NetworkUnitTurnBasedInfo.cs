using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkUnitTurnBasedInfo
    {
        [ProtoMember(1)]
        [LogMe]
        public bool Surprising { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public bool Surprised { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public bool ActingInSurpriseRound { get; set; }
    }
}
