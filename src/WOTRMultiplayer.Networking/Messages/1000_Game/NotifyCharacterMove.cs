using System.Numerics;
using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType(1001)]
    public class NotifyCharacterMove
    {
        [ProtoMember(1)]
        public string CharacterName { get; set; }

        [ProtoMember(2)]
        public float DestinationX { get; set; }

        [ProtoMember(3)]
        public float DestinationY { get; set; }

        [ProtoMember(4)]
        public float DestinationZ { get; set; }

        [ProtoMember(5)]
        public float Delay { get; set; }

        [ProtoMember(6)]
        public float Orientation { get; set; }
    }
}
