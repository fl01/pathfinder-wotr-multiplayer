using System.Collections.Generic;
using Kingmaker.RuleSystem;

namespace WOTRMultiplayer.MP.Entities.Rolls
{
    public class AttackRoll : NetworkDiceRollBase
    {
        public AttackWithWeaponRoll AttackWithWeapon { get; set; }

        public AttackType AttackType { get; set; }

        public AttackRoll(string initiatorId, string ruleName, NetworkDiceRollType networkDiceRollType, int totalModifierBonus)
            : base(initiatorId, ruleName, networkDiceRollType, totalModifierBonus)
        {
        }

        public override IEnumerable<string> GetUniquinessIdentifiers()
        {
            var attackWithWeaponId = AttackWithWeapon == null ? null : string.Join(IdSeparator, AttackWithWeapon?.GetUniquinessIdentifiers());

            return [AttackType.ToString(), attackWithWeaponId];
        }
    }
}
