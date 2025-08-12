using System;
using ProtoBuf;
using WOTRMultiplayer.Networking.Awaiters;

namespace WOTRMultiplayer.Networking.Messages.Requests
{
    [ProtoContract]
    [BeetleX.Packets.MessageType(9005)]
    public class AIActionRequest : IAwaitableMessage
    {
        [ProtoMember(1)]
        public string UnitId { get; set; }

        [ProtoMember(2)]
        public TimeSpan Timeout { get; set; }

        [ProtoMember(3)]
        public int ActionIndex { get; set; }

        public string GetKey()
        {
            return typeof(AIActionResponse).Name + UnitId;
        }
    }
}
