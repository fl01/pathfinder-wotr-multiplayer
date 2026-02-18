using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkGlobalMapEncounter
    {
        [ProtoMember(1)]
        [LogMe]
        public int? Seed { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string BlueprintId { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public string AvoidanceResult { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public NetworkVector3 Position { get; set; }

        [ProtoMember(5)]
        [LogMe]
        public bool IsTrader { get; set; }
    }
}
