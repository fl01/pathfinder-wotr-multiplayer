using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkCharacterStats
    {
        [ProtoMember(1)]
        [LogMe]
        public int DamageNonLethal { get; set; }
    }
}
