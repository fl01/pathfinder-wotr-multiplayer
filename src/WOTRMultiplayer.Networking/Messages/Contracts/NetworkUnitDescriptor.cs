using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkUnitDescriptor
    {
        [ProtoMember(1)]
        [LogMe]
        public int Damage { get; set; }

        [ProtoMember(2)]
        public NetworkCharacterStats Stats { get; set; }

        [ProtoMember(3)]
        public NetworkUnitState State { get; set; }
    }
}
