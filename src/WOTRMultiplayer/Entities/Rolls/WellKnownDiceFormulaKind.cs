namespace WOTRMultiplayer.Entities.Rolls
{
    public enum WellKnownDiceFormulaKind
    {
        RuleCheckCastingDefensively,
        RuleSpellResistanceCheck,
        RuleInitiativeRoll,
        RuleAttackRoll,
        RuleCheckConcentration,
        RuleSkillCheck,
        RuleDispelMagic,

        // RuleCastSpell
        SpellFailureRoll,
        ArcaneSpellFailureRoll,

        AttackParryData,
        // subtypes of RuleAttackRoll
        CriticalAttackRoll,
        FortificationAttackRoll
    }
}
