using System;
using ProtoBuf;
using WOTRMultiplayer.Networking.Awaiters;

namespace WOTRMultiplayer.Networking.Messages.Requests
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Request.DiceRollValueRequest)]
    public class DiceRollValueRequest : IAwaitableMessage
    {
        [ProtoMember(1)]
        public int RollId { get; set; }

        [ProtoMember(2)]
        public TimeSpan Timeout { get; set; }

        [ProtoMember(3)]
        public string UnitId { get; set; }

        public string GetKey()
        {
            return string.Join(":", UnitId , RollId.ToString());
        }
    }
}
