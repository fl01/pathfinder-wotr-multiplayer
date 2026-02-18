using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyGlobalMapTravelerModeChanged)]
    public class NotifyGlobalMapTravelerModeChanged
    {
        [ProtoMember(1)]
        [LogMe]
        public long PlayerId { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string TravelerMode { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public bool MustBeEnforced { get; set; }
    }
}
