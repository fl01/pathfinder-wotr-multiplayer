using System.Collections.Generic;
using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages
{
    [ProtoContract]
    public class NetworkLootContainer
    {
        [ProtoMember(1)]
        public string Id { get; set; }

        [ProtoMember(2)]
        public NetworkVector3 Position { get; set; }

        [ProtoMember(3)]
        public bool IsMapObject { get; set; }

        [ProtoMember(4)]
        public List<NetworkLootItem> Items { get; set; } = [];

    }
}
