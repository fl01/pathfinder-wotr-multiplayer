using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyAbilityUse)]
    public class NotifyAbilityUse
    {
        [ProtoMember(1)]
        public NetworkAbility Ability { get; set; }
    }
}
