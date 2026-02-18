using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingSpellRemoved)]
    public class NotifyLevelingSpellRemoved
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkLevelingSpell Spell { get; set; }
    }
}
