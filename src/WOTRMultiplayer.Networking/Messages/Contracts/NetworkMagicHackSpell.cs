using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkMagicHackSpell
    {
        [ProtoMember(1)]
        [LogMe]
        public int Index { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string SpellbookId { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public int SpellLevel { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public string UnitId { get; set; }

        [ProtoMember(5)]
        [LogMe]
        public string AbilityBlueprintId { get; set; }

        [ProtoMember(6)]
        [LogMe]
        public string TouchBlueprintId { get; set; }

        [ProtoMember(7)]
        [LogMe]
        public string DefaultBlueprintId { get; set; }

        [ProtoMember(8)]
        [LogMe]
        public NetworkMagicHackData MagicHackData { get; set; }
    }
}
