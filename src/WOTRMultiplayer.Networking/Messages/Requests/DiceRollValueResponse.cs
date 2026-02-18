using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Awaiters;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Requests
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Request.DiceRollValueResponse)]
    public class DiceRollValueResponse : IAwaitableResponse
    {
        [ProtoMember(1)]
        [LogMe]
        public int RollId { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public NetworkRollValue RollValue { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public string UnitId { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public long PlayerId { get; set; }

        public string GetKey()
        {
            return string.Join(":", PlayerId, UnitId, RollId.ToString());
        }
    }
}
