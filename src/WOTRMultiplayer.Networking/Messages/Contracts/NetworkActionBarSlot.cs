using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkActionBarSlot
    {
        [ProtoMember(1)]
        [LogMe]
        public int Index { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string UnitId { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public NetworkItem Item { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public NetworkAbility Ability { get; set; }

        [ProtoMember(5)]
        [LogMe]
        public NetworkActivatableAbility ActivatableAbility { get; set; }
    }
}
