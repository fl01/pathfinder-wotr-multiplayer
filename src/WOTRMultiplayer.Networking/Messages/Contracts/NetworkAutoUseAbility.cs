using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkAutoUseAbility
    {
        [ProtoMember(1)]
        public NetworkAbility Ability { get; set; }

        [ProtoMember(2)]
        public string UnitId { get; set; }
    }
}
