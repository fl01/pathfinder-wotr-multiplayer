using System.Collections.Generic;

namespace WOTRMultiplayer.MP.Entities.Rolls
{
    public class FortificationAttackRoll : NetworkDiceRollBase
    {
        public int FortificationChance { get; set; }

        public AttackRoll AttackRoll { get; set; }

        public FortificationAttackRoll(string initiatorId, string ruleName, NetworkDiceRollType networkDiceRollType, int totalModifierBonus)
            : base(initiatorId, ruleName, networkDiceRollType, totalModifierBonus)
        {
        }

        public override IEnumerable<string> GetUniquinessIdentifiers()
        {
            var attackIdentifier = AttackRoll == null ? null : string.Join(IdSeparator, AttackRoll.GetUniquinessIdentifiers());

            return ["@", FortificationChance.ToString(), "@", attackIdentifier];
        }
    }
}
