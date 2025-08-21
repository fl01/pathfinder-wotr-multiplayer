using ProtoBuf;
using WOTRMultiplayer.Networking.Awaiters;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Requests
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Request.DiceRollValueResponse)]
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
