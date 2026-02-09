using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyUnitAutoUseAbilityChanged)]
    public class NotifyUnitAutoUseAbilityChanged
    {
        [ProtoMember(1)]
        public NetworkAutoUseAbility AutoUse { get; set; }
    }
}
