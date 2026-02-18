using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyAbilityUsed)]
    public class NotifyAbilityUsed
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkAbilityUse AbilityUse { get; set; }
    }
}
