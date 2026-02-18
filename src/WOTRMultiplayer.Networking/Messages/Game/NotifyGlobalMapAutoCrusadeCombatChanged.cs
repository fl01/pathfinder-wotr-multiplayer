using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyGlobalMapAutoCrusadeCombatChanged)]
    public class NotifyGlobalMapAutoCrusadeCombatChanged
    {
        [ProtoMember(1)]
        [LogMe]
        public bool IsEnabled { get; set; }
    }
}
