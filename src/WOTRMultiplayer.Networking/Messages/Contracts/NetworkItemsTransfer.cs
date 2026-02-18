using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkItemsTransfer
    {
        [ProtoMember(1)]
        [LogMe]
        public List<NetworkItem> Items { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public NetworkLootableEntity Source { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public NetworkLootableEntity Destination { get; set; }
    }
}
