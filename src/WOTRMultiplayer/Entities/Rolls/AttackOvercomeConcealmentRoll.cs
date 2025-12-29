using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.Rolls
{
    public class AttackOvercomeConcealmentRoll : NetworkDiceRollBase
    {
        public int MissChance { get; set; }

        public AttackRoll AttackRoll { get; set; }

        public AttackOvercomeConcealmentRoll(string initiatorId, string ruleName, NetworkDiceRollType networkDiceRollType, int totalModifierBonus)
            : base(initiatorId, ruleName, networkDiceRollType, totalModifierBonus)
        {
        }

        public override IEnumerable<string> GetUniquinessIdentifiers()
        {
            var attackIdentifier = AttackRoll == null ? null : string.Join(IdSeparator, AttackRoll.GetUniquinessIdentifiers());

            return ["@", MissChance.ToString(), "@", attackIdentifier];
        }
    }
}
