using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Awaiters;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Requests
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Request.RandomEncounterContextResponse)]
    public class RandomEncounterContextResponse : IAwaitableResponse
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkRandomEncounter Encounter { get; set; }

        public string GetKey()
        {
            return typeof(RandomEncounterContextResponse).Name;
        }
    }
}
