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

        // subtypes of RuleAttackRoll
        CriticalAttackRoll,
        FortificationAttackRoll
    }
}
