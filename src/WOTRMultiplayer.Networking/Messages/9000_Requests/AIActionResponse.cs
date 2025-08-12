using ProtoBuf;
using WOTRMultiplayer.Networking.Awaiters;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Requests
{
    [ProtoContract]
    [BeetleX.Packets.MessageType(9006)]
    public class AIActionResponse : IAwaitableMessage
    {
        [ProtoMember(1)]
        public string UnitId { get; set; }

        [ProtoMember(2)]
        public NetworkAIAction Action { get; set; }

        public string GetKey()
        {
            return typeof(AIActionResponse).Name + UnitId;
        }
    }
}
