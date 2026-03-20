using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingMythicClassSelected)]
    public class NotifyLevelingMythicClassSelected : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public string MythicClassId { get; set; }
    }
}
