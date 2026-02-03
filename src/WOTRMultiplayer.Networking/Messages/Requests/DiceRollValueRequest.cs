using System;
using ProtoBuf;
using WOTRMultiplayer.Networking.Awaiters;

namespace WOTRMultiplayer.Networking.Messages.Requests
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Request.DiceRollValueRequest)]
    public class DiceRollValueRequest : IAwaitableRequest
    {
        [ProtoMember(1)]
        public int RollId { get; set; }

        [ProtoMember(2)]
        public TimeSpan Timeout { get; set; }

        [ProtoMember(3)]
        public string UnitId { get; set; }

        [ProtoMember(4)]
        public long PlayerId { get; set; }

        [ProtoMember(5)]
        public string CombatTurnUnitId { get; set; }

        [ProtoMember(6)]
        public string RuleName { get; set; }

        public string GetKey()
        {
            return string.Join(":", PlayerId, UnitId, RollId.ToString());
        }
    }
}
