namespace WOTRMultiplayer.MP.Entities.Rolls
{
    public class AttackWithWeaponRoll : NetworkDiceRoll
    {
        public int AttackNumber { get; set; }

        public int CombatRound { get; set; }

        public bool IsAttackOfOpportunity { get; set; }

        public string TargetId { get; set; }

        public bool ExtraAttack { get; set; }

        public bool IsFirstAttack { get; set; }

        public int AttacksCount { get; set; }
        public bool IsCriticalRoll { get; set; }

        public override string GetIdString()
        {
            return string.Join(IdSeparator, base.GetIdString(), AttackNumber.ToString(), CombatRound.ToString(), IsAttackOfOpportunity
                , TargetId, ExtraAttack, IsFirstAttack, AttacksCount, IsCriticalRoll);
        }
    }
}
