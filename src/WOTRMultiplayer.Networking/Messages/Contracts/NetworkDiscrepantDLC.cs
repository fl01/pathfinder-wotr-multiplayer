using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkDiscrepantDLC
    {
        [ProtoMember(1)]
        public NetworkDLC DLC { get; set; }

        [ProtoMember(2)]
        public string Reason { get; set; }
    }
}
