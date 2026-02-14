using Kingmaker.RuleSystem;
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

        bool OnBeforeRuleSavingThrowRoll(RuleSavingThrow ruleSavingThrow);
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

        bool OnBeforeRollRuleHealDamage(RuleHealDamage ruleHealDamage, bool isTacticalCombat, DiceFormula diceFormula);

        void OnAfterRollRuleHealDamage(RuleHealDamage ruleHealDamage, int result, bool isTacticalCombat);

        bool OnBeforeRuleCastSpellRoll(RuleCastSpell ruleCastSpell, bool isSpellFailure);
        void OnAfterRuleCastSpellTrigger(RuleCastSpell ruleCastSpell);

        bool OnBeforeRuleEnterStealthRoll(RuleEnterStealth ruleEnterStealth);
        void OnAfterRuleEnterStealthTrigger(RuleEnterStealth ruleEnterStealth);

        void OnBeforeRuleRollChanceTrigger(RuleRollChance ruleRollChance);
        void OnAfterRuleRollChanceTrigger(RuleRollChance ruleRollChance);

        int? OnBeforeRuleDealStatDamageRoll(RuleDealStatDamage ruleDealStatDamage, int criticalModifier);
        void OnAfterRuleDealStatDamageRoll(RuleDealStatDamage ruleDealStatDamage, RuleRollD100 damageRoll, int criticalModifier);

        bool OnBeforeRuleDrainEnergyRoll(RuleDrainEnergy ruleDrainEnergy, RuleRollDice rollD20);
        void OnAfterRuleDrainEnergyRoll(RuleDrainEnergy ruleDrainEnergy, RuleRollDice rollD20);

        bool OnBeforeRuleCombatManeuverRoll(RuleCombatManeuver ruleCombatManeuver);
        void OnAfterRuleCombatManeuverRoll(RuleCombatManeuver ruleCombatManeuver, RuleRollD20 rollD20);
    }
}
