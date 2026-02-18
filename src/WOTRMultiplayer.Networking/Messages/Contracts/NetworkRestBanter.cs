using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkRestBanter
    {
        [ProtoMember(1)]
        [LogMe]
        public string Key { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string SpeakerUnitId { get; set; }
    }
}
