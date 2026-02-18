using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingGenderSelected)]
    public class NotifyLevelingGenderSelected
    {
        [ProtoMember(1)]
        [LogMe]
        public string GenderId { get; set; }
    }
}
