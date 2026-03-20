using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyUnitAutoUseAbilityChanged)]
    public class NotifyUnitAutoUseAbilityChanged : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkAutoUseAbility AutoUse { get; set; }
    }
}
