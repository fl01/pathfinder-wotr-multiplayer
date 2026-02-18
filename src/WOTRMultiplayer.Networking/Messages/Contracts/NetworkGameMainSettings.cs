using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkGameMainSettings
    {
        [ProtoMember(1)]
        [LogMe]
        public bool LootInCombat { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public bool QuickMovement { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public bool? AutofillActionbarSlots { get; set; }
    }
}
