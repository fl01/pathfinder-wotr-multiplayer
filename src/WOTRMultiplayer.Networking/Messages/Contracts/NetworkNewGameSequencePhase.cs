using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkNewGameSequencePhase
    {
        [ProtoMember(1)]
        [LogMe]
        public string Type { get; set; }
    }
}
