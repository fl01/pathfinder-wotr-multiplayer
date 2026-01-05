using System;
using ProtoBuf;
using WOTRMultiplayer.Networking.Awaiters;

namespace WOTRMultiplayer.Networking.Messages.Requests
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Request.RandomEncounterContextRequest)]
    public class RandomEncounterContextRequest : IAwaitableMessage
    {
        [ProtoMember(1)]
        public TimeSpan Timeout { get; set; }

        [ProtoMember(2)]
        public int SleepPhase { get; set; }

        public string GetKey()
        {
            return typeof(RandomEncounterContextResponse).Name;
        }
    }
}
