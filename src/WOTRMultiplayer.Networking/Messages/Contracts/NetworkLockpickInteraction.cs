using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkLockpickInteraction
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkMapObject MapObject { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public List<string> Units { get; set; } = [];

        [ProtoMember(3)]
        [LogMe]
        public string LockpickType { get; set; }
    }
}
