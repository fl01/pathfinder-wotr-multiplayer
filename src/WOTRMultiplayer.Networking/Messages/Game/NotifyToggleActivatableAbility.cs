using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyToggleActivatableAbility)]
    public class NotifyToggleActivatableAbility
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkActivatableAbility Ability { get; set; }
    }
}
