using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyMapObjectCombinePartInteracted)]
    public class NotifyMapObjectCombinePartInteracted : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkMapObject MapObject { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public int PartIndex { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public string UnitId { get; set; }
    }
}
