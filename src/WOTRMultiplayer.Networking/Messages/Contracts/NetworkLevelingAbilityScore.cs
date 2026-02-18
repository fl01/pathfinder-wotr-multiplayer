using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkLevelingAbilityScore
    {
        [ProtoMember(1)]
        [LogMe]
        public string StatType { get; set; }
    }
}
