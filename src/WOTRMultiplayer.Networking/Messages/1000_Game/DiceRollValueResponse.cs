using ProtoBuf;
using WOTRMultiplayer.Networking.Awaiters;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType(1007)]
    public class DiceRollValueResponse : IAwaitableMessage
    {
        [ProtoMember(1)]
        public int RollId { get; set; }

        [ProtoMember(2)]
        public NetworkRollValue RollValue { get; set; }

        [ProtoMember(3)]
        public string UnitId { get; set; }

        public string GetKey()
        {
            return string.Join(":", UnitId, RollId.ToString());
        }
    }
}
