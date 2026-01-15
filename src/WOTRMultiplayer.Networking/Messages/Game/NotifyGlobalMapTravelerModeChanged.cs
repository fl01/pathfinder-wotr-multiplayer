using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyGlobalMapTravelerModeChanged)]
    public class NotifyGlobalMapTravelerModeChanged
    {
        [ProtoMember(1)]
        public long PlayerId { get; set; }

        [ProtoMember(2)]
        public string TravelerMode { get; set; }

        [ProtoMember(3)]
        public bool MustBeEnforced { get; set; }
    }
}
