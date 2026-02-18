using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyGroupChangerUnitClicked)]
    public class NotifyGroupChangerUnitClicked
    {
        [ProtoMember(1)]
        [LogMe]
        public string UnitId { get; set; }
    }
}
