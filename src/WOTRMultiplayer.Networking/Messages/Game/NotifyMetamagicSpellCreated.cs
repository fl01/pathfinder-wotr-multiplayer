using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyMetamagicSpellCreated)]
    public class NotifyMetamagicSpellCreated : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkMetamagicSpell MetamagicSpell { get; set; }
    }
}
