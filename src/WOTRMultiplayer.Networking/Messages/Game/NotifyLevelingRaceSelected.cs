using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingRaceSelected)]
    public class NotifyLevelingRaceSelected
    {
        [ProtoMember(1)]
        [LogMe]
        public string RaceId { get; set; }
    }
}
