using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkAutoUseAbility
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkAbility Ability { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string UnitId { get; set; }
    }
}
