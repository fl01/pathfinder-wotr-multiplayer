using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [MessageType((int)MessageTypes.Game.NotifyMagicHackSpellCreated)]
    public class NotifyMagicHackSpellCreated : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkMagicHackSpell MagicHackSpell { get; set; }
    }
}
