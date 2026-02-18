using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkDamageRollValue
    {
        [ProtoMember(1)]
        [LogMe]
        public float TacticalCombatDRModifier { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public int? MaximumDamage { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public int ValueWithoutReduction { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public int RollAndBonusValue { get; set; }

        [ProtoMember(5)]
        [LogMe]
        public int RollResult { get; set; }
    }
}
