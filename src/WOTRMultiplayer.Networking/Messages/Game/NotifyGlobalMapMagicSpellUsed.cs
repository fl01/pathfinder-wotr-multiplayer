using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyGlobalMapMagicSpellUsed)]
    public class NotifyGlobalMapMagicSpellUsed
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkGlobalMapMagicSpell Spell { get; set; }
    }
}
