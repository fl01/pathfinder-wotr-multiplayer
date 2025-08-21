using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingSpellChosen)]
    public class NotifyLevelingSpellChosen
    {
        [ProtoMember(1)]
        public NetworkLevelingSpell Spell { get; set; }
    }
}
