namespace WOTRMultiplayer.Entities.Rolls.Claiming.Values
{
    public class NetworkDamageRollValue
    {
        public float TacticalCombatDRModifier { get; set; }

        public int? MaximumDamage { get; set; }

        public int ValueWithoutReduction { get; set; }

        public int RollAndBonusValue { get; set; }

        public int RollResult { get; set; }
    }
}
