using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkCharacter
    {
        [ProtoMember(1)]
        [LogMe]
        public string Name { get; set; }

        [ProtoMember(2)]
        public string Portrait { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public string UnitId { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public long OwnerId { get; set; }

        [ProtoMember(5)]
        [LogMe]
        public string CustomPortraitId { get; set; }

        [ProtoMember(6)]
        [LogMe]
        public int? Index { get; set; }

        public override string ToString()
        {
            return Name ?? Portrait;
        }
    }
}
