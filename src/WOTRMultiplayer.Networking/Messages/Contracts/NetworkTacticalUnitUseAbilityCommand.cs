using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkTacticalUnitUseAbilityCommand
    {
        [ProtoMember(1)]
        public NetworkAbility Ability { get; set; }
    }
}
