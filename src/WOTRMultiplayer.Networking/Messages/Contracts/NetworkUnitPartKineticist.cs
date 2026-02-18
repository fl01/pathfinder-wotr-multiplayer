using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkUnitPartKineticist
    {
        [ProtoMember(1)]
        [LogMe]
        public int AcceptedBurn { get; set; }
    }
}
