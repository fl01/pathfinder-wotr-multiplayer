using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkAreaTransition
    {
        [ProtoMember(1)]
        [LogMe]
        public string AreaExitId { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public bool IsActionsTransition { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public NetworkArea From { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public NetworkArea To { get; set; }
    }
}
