using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyUnitStealthChanged)]
    public class NotifyUnitStealthChanged : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public string UnitId { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public bool IsEnabled { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public bool IsForced { get; set; }
    }
}
