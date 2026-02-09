using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkGlobalMapEncounter
    {
        [ProtoMember(1)]
        public int? Seed { get; set; }

        [ProtoMember(2)]
        public string BlueprintId { get; set; }

        [ProtoMember(3)]
        public string AvoidanceResult { get; set; }

        [ProtoMember(4)]
        public NetworkVector3 Position { get; set; }

        [ProtoMember(5)]
        public bool IsTrader { get; set; }
    }
}
