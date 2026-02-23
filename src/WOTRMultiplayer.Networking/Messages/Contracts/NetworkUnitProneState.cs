using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkUnitProneState
    {
        [ProtoMember(1)]
        [LogMe]
        public bool Active { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public bool ShouldBeActive { get; set; }
    }
}
