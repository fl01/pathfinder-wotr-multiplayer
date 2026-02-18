using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkUnitState
    {
        [ProtoMember(1)]
        [LogMe]
        public bool IsCharging { get; set; }
    }
}
