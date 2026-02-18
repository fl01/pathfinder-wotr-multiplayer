using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkLevelingPhase
    {
        [ProtoMember(1)]
        [LogMe]
        public int Index { get; set; }
    }
}
