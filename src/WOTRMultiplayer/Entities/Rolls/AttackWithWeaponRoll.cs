using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.Rolls
{
    public class AttackWithWeaponRoll : NetworkDiceRollBase
    {
        public int AttackNumber { get; set; }

        public bool IsAttackOfOpportunity { get; set; }

        public string TargetId { get; set; }

        public bool ExtraAttack { get; set; }

        public bool IsFirstAttack { get; set; }

        public AttackWithWeaponRoll(string initiatorId, string ruleName, NetworkDiceRollType networkDiceRollType, int totalModifierBonus)
            : base(initiatorId, ruleName, networkDiceRollType, totalModifierBonus)
        {
        }

        public override IEnumerable<string> GetUniquinessIdentifiers()
        {
            return [AttackNumber.ToString(), IsAttackOfOpportunity.ToString(), TargetId, ExtraAttack.ToString(), IsFirstAttack.ToString()];
        }
    }
}