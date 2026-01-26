using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyGlobalMapMagicSpellUsed)]
    public class NotifyGlobalMapMagicSpellUsed
    {
        [ProtoMember(1)]
        public NetworkGlobalMapMagicSpell Spell { get; set; }
    }
}
