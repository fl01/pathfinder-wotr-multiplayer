using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.RuleSystem.Rules.Damage;

namespace WOTRMultiplayer.Abstractions
{
    public interface IMultiplayerRollsProcessor
    {
        bool OnBeforeRuleCalculateDamageTrigger(RuleCalculateDamage ruleCalculateDamage);
        void OnAfterRuleCalculateDamageTrigger(RuleCalculateDamage ruleCalculateDamage);

        bool OnBeforeRuleAttackOvercomeConcealmentRoll(RuleAttackRoll ruleAttackRoll);
        void OnAfterRuleAttackOvercomeConcealmentRoll(RuleAttackRoll ruleAttackRoll);
        bool OnBeforeRuleAttackFortificationRoll(RuleAttackRoll ruleAttackRoll);
        bool OnBeforeRuleAttackRoll(RuleAttackRoll ruleAttackRoll);
        void OnAfterRuleAttackRollTrigger(RuleAttackRoll ruleAttackRoll);

        void OnBeforeRuleSavingThrowRoll(RuleSavingThrow ruleSavingThrow);
        void OnAfterRuleSavingThrowTrigger(RuleSavingThrow ruleSavingThrow);

        bool OnBeforeRuleSpellResistanceCheckRoll(RuleSpellResistanceCheck ruleSpellResistanceCheck);
        void OnAfterRuleSpellResistanceCheckTrigger(RuleSpellResistanceCheck ruleSpellResistanceCheck);

        bool OnBeforeRuleSkillCheckRoll(RuleSkillCheck ruleSkillCheck);
        void OnAfterRuleSkillCheckTrigger(RuleSkillCheck ruleSkillCheck);

        bool OnBeforeRuleInitiativeRoll(RuleInitiativeRoll ruleInitiativeRoll);
        void OnAfterRuleInitiativeRollTrigger(RuleInitiativeRoll ruleInitiativeRoll);

        bool OnBeforeRuleCheckConcentrationRoll(RuleCheckConcentration ruleCheckConcentration);
        void OnAfterRuleCheckConcentrationTrigger(RuleCheckConcentration ruleCheckConcentration);

        bool OnBeforeRuleConcealmentCheckTrigger(RuleConcealmentCheck ruleConcealmentCheck);
        void OnAfterRuleConcealmentCheckTrigger(RuleConcealmentCheck ruleConcealmentCheck);

        bool OnBeforeParryDataTrigger(RuleAttackRoll.ParryData parryData);
        void OnAfterParryDataTrigger(RuleAttackRoll.ParryData parryData);

        bool OnBeforeRuleDispelMagicRoll(RuleDispelMagic ruleDispelMagic);
        void OnAfterRuleDispelMagicTrigger(RuleDispelMagic ruleDispelMagic);

        bool OnBeforeRuleCheckCastingDefensivelyRoll(RuleCheckCastingDefensively ruleCheckCastingDefensively);
        void OnAfterRuleCheckCastingDefensivelyTrigger(RuleCheckCastingDefensively ruleCheckCastingDefensively);

        bool OnBeforeRollRuleHealDamage(RuleHealDamage ruleHealDamage, int unitsCount, bool isTacticalCombat);
        void OnAfterRollRuleHealDamage(RuleHealDamage ruleHealDamage, int unitsCount, int result, bool isTacticalCombat);

        bool OnBeforeRuleCastSpellRoll(RuleCastSpell ruleCastSpell, bool isSpellFailure);
        void OnAfterRuleCastSpellTrigger(RuleCastSpell ruleCastSpell);
    }
}
