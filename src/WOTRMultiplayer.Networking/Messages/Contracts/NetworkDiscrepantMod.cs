using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkDiscrepantMod
    {
        [ProtoMember(1)]
        public NetworkMod Mod { get; set; }

        [ProtoMember(2)]
        public string Reason { get; set; }
    }
}
