using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkGlobalMapResourceOrder
    {
        [ProtoMember(1)]
        public int MaterialCount { get; set; }

        [ProtoMember(2)]
        public int FinanceCount { get; set; }

        [ProtoMember(3)]
        public int FinalCost { get; set; }
    }
}
