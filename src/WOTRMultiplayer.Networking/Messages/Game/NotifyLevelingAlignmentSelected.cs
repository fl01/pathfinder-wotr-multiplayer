using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingAlignmentSelected)]
    public class NotifyLevelingAlignmentSelected : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public string AlignmentId { get; set; }
    }
}
