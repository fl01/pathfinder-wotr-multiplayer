using System.Collections.Generic;
using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkAreaEffect
    {
        [ProtoMember(1)]
        public string Id { get; set; }

        [ProtoMember(2)]
        public string Name { get; set; }

        [ProtoMember(3)]
        public NetworkVector3 Position { get; set; }

        [ProtoMember(4)]
        public List<string> UnitsInside { get; set; } = [];

        [ProtoMember(5)]
        public string Type { get; set; }

        public override string ToString()
        {
            return Id.ToString();
        }
    }
}
