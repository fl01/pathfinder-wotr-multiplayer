using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkAreaTransition
    {
        [ProtoMember(1)]
        public string AreaExitId { get; set; }

        [ProtoMember(2)]
        public bool IsActionsTransition { get; set; }

        [ProtoMember(3)]
        public NetworkArea From { get; set; }

        [ProtoMember(4)]
        public NetworkArea To { get; set; }
    }
}
