using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyUnitStealthChanged)]
    public class NotifyUnitStealthChanged
    {
        [ProtoMember(1)]
        public string UnitId { get; set; }

        [ProtoMember(2)]
        public bool IsEnabled { get; set; }

        [ProtoMember(3)]
        public bool IsForced { get; set; }
    }
}
