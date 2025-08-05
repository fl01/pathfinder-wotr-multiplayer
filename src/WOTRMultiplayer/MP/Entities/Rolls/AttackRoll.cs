using System.Collections.Generic;

namespace WOTRMultiplayer.MP.Entities.Rolls
{
    public class AttackRoll : NetworkDiceRollBase
    {
        public AttackWithWeaponRoll AttackWithWeapon { get; set; }

        public string AttackType { get; set; }

        public string TargetId { get; set; }

        public bool IsCriticalRoll { get; set; }

        public AttackRoll(string initiatorId, string ruleName, NetworkDiceRollType networkDiceRollType, int totalModifierBonus)
            : base(initiatorId, ruleName, networkDiceRollType, totalModifierBonus)
        {
        }

        public override IEnumerable<string> GetUniquinessIdentifiers()
        {
            var attackWithWeaponId = AttackWithWeapon == null ? null : string.Join(IdSeparator, AttackWithWeapon?.GetUniquinessIdentifiers());

            return ["@", AttackType, TargetId, IsCriticalRoll.ToString(), "@", attackWithWeaponId];
        }
    }
}
