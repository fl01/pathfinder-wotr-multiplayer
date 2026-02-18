using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkGlobalMapResourceOrder
    {
        [ProtoMember(1)]
        [LogMe]
        public int MaterialCount { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public int FinanceCount { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public int FinalCost { get; set; }
    }
}
