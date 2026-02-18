using System;
using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Awaiters;

namespace WOTRMultiplayer.Networking.Messages.Requests
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Request.DiceRollValueRequest)]
    public class DiceRollValueRequest : IAwaitableRequest
    {
        [ProtoMember(1)]
        [LogMe]
        public int RollId { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public TimeSpan Timeout { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public string UnitId { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public long PlayerId { get; set; }

        [ProtoMember(5)]
        [LogMe]
        public string CombatTurnUnitId { get; set; }

        [ProtoMember(6)]
        [LogMe]
        public string RuleName { get; set; }

        public string GetKey()
        {
            return string.Join(":", PlayerId, UnitId, RollId.ToString());
        }
    }
}
