using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType(1042)]
    public class NotifyVendorItemTransferred
    {
        [ProtoMember(1)]
        public NetworkVendorItemTransfer ItemTransfer { get; set; }
    }
}
