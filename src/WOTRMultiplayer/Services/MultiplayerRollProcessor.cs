using System;
using System.Reflection;
using Kingmaker.GameModes;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.RuleSystem.Rules.Damage;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.Entities.Rolls;
using WOTRMultiplayer.Extensions;

namespace WOTRMultiplayer.Services
{
    public class MultiplayerRollProcessor : IMultiplayerRollsProcessor
    {
        private readonly ILogger<MultiplayerRollProcessor> _logger;
        private readonly IGameInteractionService _gameInteractionService;
        private readonly ICombatInteractionService _combatInteractionService;
        private readonly IMultiplayerActorAccessor _multiplayerActorAccessor;
        private readonly IValueGenerator _valueGenerator;

        public MultiplayerRollProcessor(
            ILogger<MultiplayerRollProcessor> logger,
            IGameInteractionService gameInteractionService,
            ICombatInteractionService combatInteractionService,
            IMultiplayerActorAccessor multiplayerActorAccessor,
            IValueGenerator valueGenerator)
        {
            _logger = logger;
            _gameInteractionService = gameInteractionService;
            _combatInteractionService = combatInteractionService;
            _multiplayerActorAccessor = multiplayerActorAccessor;
            _valueGenerator = valueGenerator;
        }

        public int? OnBeforeRuleCalculateDamageRoll(RuleCalculateDamage ruleCalculateDamage, DiceFormula diceFormula)
        {
            try
            {
                if (!IsMeaningfulRoll(ruleCalculateDamage) || diceFormula.Rolls == 0 && diceFormula.Dice == DiceType.Zero)
                {
                    return null;
                }

                var roll = RollDamage(ruleCalculateDamage, diceFormula);
                return roll;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error before damage rule trigger");
                throw;
            }
        }

        public bool OnBeforeRollRuleHealDamage(RuleHealDamage ruleHealDamage, DiceFormula diceFormula)
        {
            try
            {
                if (!IsMeaningfulRoll(ruleHealDamage) || diceFormula.Rolls == 0 && diceFormula.Dice == DiceType.Zero)
                {
                    return true;
                }

                var result = RollHealDamage(ruleHealDamage, diceFormula);
                ruleHealDamage.RollResult = result;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error before heal rule trigger");
                throw;
            }
        }

        public int? OnBeforeRuleDealStatDamageRoll(RuleDealStatDamage ruleDealStatDamage, DiceFormula damageFormula, int criticalModifier)
        {
            try
            {
                if (!IsMeaningfulRoll(ruleDealStatDamage))
                {
                    return null;
                }

                var roll = CreateDealStatDamageRoll(NetworkDiceRollType.Damage, ruleDealStatDamage, criticalModifier);
                var d100 = RollDealStatDamage(ruleDealStatDamage, damageFormula, criticalModifier);
                if (d100 == null)
                {
                    return null;
                }

                return d100;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public int? OnRollDice(RuleRollDice ruleRollDice)
        {
            try
            {
                if (ruleRollDice.IsFake)
                {
                    return null;
                }

                if (ruleRollDice.DiceFormula.Dice == DiceType.Zero && ruleRollDice.DiceFormula.Rolls == 0)
                {
                    return null;
                }

                // current is always RuleRollDice instance
                var previousEvent = Rulebook.CurrentContext?.PreviousEvent;
                if (previousEvent == null || !IsMeaningfulRoll(previousEvent))
                {
                    return null;
                }

                NetworkDiceRollBase diceRoll = previousEvent switch
                {
                    RuleAttackRoll parryRoll when parryRoll.Parry != null && parryRoll.IsHit => CreateParryRoll(NetworkDiceRollType.Hit, parryRoll.Parry),
                    RuleAttackRoll attackRoll => CreateAttackRoll(attackRoll.IsCriticalRoll ? NetworkDiceRollType.Critical : NetworkDiceRollType.Hit, attackRoll, attackRoll.IsCriticalRoll),
                    RuleSpellResistanceCheck ruleSpellResistanceCheck => CreateSpellResistanceCheckRoll(NetworkDiceRollType.Hit, ruleSpellResistanceCheck),
                    RuleInitiativeRoll ruleInitiativeRoll => CreateInitiativeRoll(NetworkDiceRollType.Hit, ruleInitiativeRoll),
                    RuleDispelMagic ruleDispelMagic => CreateDispelMagicRoll(NetworkDiceRollType.Hit, ruleDispelMagic),
                    RuleCheckCastingDefensively ruleCheckCastingDefensively => CreateCastingDefensivelyRoll(NetworkDiceRollType.Hit, ruleCheckCastingDefensively),
                    RuleSavingThrow ruleSavingThrow => CreateSavingThrowRoll(NetworkDiceRollType.Hit, ruleSavingThrow),
                    RuleSkillCheck ruleSkillCheck => CreateSkillCheckRoll(NetworkDiceRollType.Hit, ruleSkillCheck),
                    RuleCombatManeuver ruleCombatManeuver => CreateCombatManeuverRoll(NetworkDiceRollType.Hit, ruleCombatManeuver),
                    RuleCastSpell ruleCastSpell => CreateCastSpellRoll(NetworkDiceRollType.Hit, ruleCastSpell, ruleCastSpell.SpellFailureChance > 0 && ruleCastSpell.ArcaneSpellFailureChance == 0),
                    RuleEnterStealth ruleEnterStealth => CreateEnterStealthRoll(NetworkDiceRollType.Hit, ruleEnterStealth),
                    RuleDrainEnergy ruleDrainEnergy => CreateDrainEnergyRoll(NetworkDiceRollType.Hit, ruleDrainEnergy, ruleRollDice),
                    RuleConcealmentCheck ruleConcealmentCheck => CreateConcealmentRoll(NetworkDiceRollType.Hit, ruleConcealmentCheck),
                    RuleCheckConcentration ruleCheckConcentration => CreateConcentrationRoll(NetworkDiceRollType.Hit, ruleCheckConcentration),
                    RuleRollChance ruleRollChance => CreateChanceRoll(NetworkDiceRollType.Hit, ruleRollChance),
                    _ => null
                };

                if (diceRoll == null)
                {
                    _logger.LogWarning("Roll event is not handled. Event={Event}", previousEvent?.GetType().Name);
                    return null;
                }

                var value = RollDice(diceRoll, ruleRollDice.DiceFormula);
                return value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while rolling dice");
                throw;
            }
        }

        private bool IsMeaningfulRoll(object rule)
        {
            const string HelperUnitPrefix = "description-helper-";
            if (
                rule is RulebookTargetEvent rulebookTargetEvent && !string.IsNullOrEmpty(rulebookTargetEvent.Target?.UniqueId) && rulebookTargetEvent.Target.UniqueId.StartsWith(HelperUnitPrefix, StringComparison.OrdinalIgnoreCase)
                    ||
                rule is RulebookEvent rulebookEvent && !string.IsNullOrEmpty(rulebookEvent.Initiator?.UniqueId) && rulebookEvent.Initiator.UniqueId.StartsWith(HelperUnitPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var gameMode = _gameInteractionService.CurrentGameMode;
            if (gameMode == GameModeType.Dialog)
            {
                return rule is RuleSkillCheck or RuleSavingThrow or RuleCalculateDamage;
            }

            if (gameMode == GameModeType.Cutscene || gameMode == GameModeType.CutsceneGlobalMap)
            {
                var areaName = _multiplayerActorAccessor.Current.CurrentArea?.Name;
                var isMeaningfulCutsceneRoll = rule switch
                {
                    RulebookTargetEvent rulebookTarget when rule is RuleSavingThrow or RuleSpellResistanceCheck => IsControlledCharacterTargeted(rulebookTarget),
                    RuleCalculateDamage => string.Equals(areaName, "EstrodTower", StringComparison.OrdinalIgnoreCase), // using columns to damage enemies
                    RuleSavingThrow => string.Equals(areaName, "Daeran_Q2_HeavenDoorstep", StringComparison.OrdinalIgnoreCase), // distraction (drinking)
                    _ => false
                };
                return isMeaningfulCutsceneRoll;
            }

            if (_combatInteractionService.IsInCrusadeTacticalCombat())
            {
                // ignore damage ranges shown on hover
                return rule switch
                {
                    RuleAttackRoll attackRoll => !attackRoll.IsFake,
                    RuleCalculateDamage calculateDamage when calculateDamage.ParentRule is RuleDealDamage dealDamage => !dealDamage.IsFake,
                    _ => true,
                };
            }

            switch (rule)
            {
                case RuleCalculateDamage:
                case RuleHealDamage:
                case RuleDealDamage:
                    var targetEvent = (RulebookTargetEvent)rule;
                    var isMeaningfulTargetEvent = IsControlledCharacterTargeted(targetEvent);
                    return isMeaningfulTargetEvent;
                // this one is used to detect stealth units. It's always rolled on the host and sent to the client as separate info to prevent sync issues (similar to other perception/inspection checks)
                case RuleCachedPerceptionCheck:
                    return false;
                default:
                    return true;
            }
        }

        private bool IsControlledCharacterTargeted(RulebookTargetEvent rulebookTargetEvent)
        {
            var initiator = rulebookTargetEvent.Initiator?.UniqueId;
            var target = rulebookTargetEvent.Target?.UniqueId;
            var affectsControlledCharacters = (_combatInteractionService.IsInCombat() && (_combatInteractionService.IsInCombat(initiator) || _combatInteractionService.IsInCombat(target)))
                || _multiplayerActorAccessor.Current.IsControlledByPlayers(initiator)
                || _multiplayerActorAccessor.Current.IsControlledByPlayers(target);

            return affectsControlledCharacters;
        }

        private NetworkDiceRollBase GetDamageRoll(RuleCalculateDamage ruleCalculateDamage)
        {
            NetworkDiceRollBase roll = ruleCalculateDamage.Reason.Rule switch
            {
                RuleAttackWithWeapon ruleAttackWithWeapon => CreateAttackWithWeaponRoll(NetworkDiceRollType.Damage, ruleAttackWithWeapon),
                RuleDealDamage ruleDealDamage => CreateUnspecifiedDamage(NetworkDiceRollType.Damage, ruleDealDamage.Calculate),
                null => CreateAbilityUse(NetworkDiceRollType.Damage, ruleCalculateDamage),
                _ => null,
            };

            return roll;
        }

        private DispelMagicRoll CreateDispelMagicRoll(NetworkDiceRollType diceRollType, RuleDispelMagic ruleDispelMagic)
        {
            var roll = new DispelMagicRoll(ruleDispelMagic.Initiator.UniqueId, ruleDispelMagic.GetType().Name, diceRollType, ruleDispelMagic.Bonus)
            {
                CasterLevel = ruleDispelMagic.CasterLevel,
                CheckType = ruleDispelMagic.Check.ToString(),
                DC = ruleDispelMagic.DC,
                Skill = ruleDispelMagic.Skill.ToString(),
                BuffName = ruleDispelMagic.Buff?.NameForAcronym,
                AreaEffectName = ruleDispelMagic.AreaEffect?.View.name
            };

            return roll;
        }

        private ParryRoll CreateParryRoll(NetworkDiceRollType diceRollType, RuleAttackRoll.ParryData parryData)
        {
            var roll = new ParryRoll(parryData.Initiator.UniqueId, parryData.GetType().Name, diceRollType, 0)
            {
                TargetId = parryData.AttackBonusRule.Target.UniqueId,
                WeaponId = parryData.AttackBonusRule.Weapon.UniqueId,
            };

            return roll;
        }

        private ConcealmentRoll CreateConcealmentRoll(NetworkDiceRollType diceRollType, RuleConcealmentCheck ruleConcealmentCheck)
        {
            var roll = new ConcealmentRoll(ruleConcealmentCheck.Initiator.UniqueId, ruleConcealmentCheck.GetType().Name, diceRollType, ruleConcealmentCheck.TotalBonusValue)
            {
                Concealment = ruleConcealmentCheck.Concealment.ToString(),
                ConcealmentValue = ruleConcealmentCheck.ConcealmentValue,
                MissChance = ruleConcealmentCheck.missChance.missChanceBase,
                TargetId = ruleConcealmentCheck.Target.UniqueId,
                IsAttack = ruleConcealmentCheck.m_Attack
            };

            return roll;
        }

        private ConcentrationRoll CreateConcentrationRoll(NetworkDiceRollType diceRollType, RuleCheckConcentration ruleCheckConcentration)
        {
            var roll = new ConcentrationRoll(ruleCheckConcentration.Initiator.UniqueId, ruleCheckConcentration.GetType().Name, diceRollType, ruleCheckConcentration.TotalBonusValue)
            {
                DC = ruleCheckConcentration.DC,
                Concentration = ruleCheckConcentration.Concentration,
                Damage = ruleCheckConcentration.Damage?.Result ?? 0,
                AbilityName = ruleCheckConcentration.Reason?.Ability?.NameForAcronym,
                AddTwiceSpellLevel = ruleCheckConcentration.AddTwiceSpellLevel
            };

            return roll;
        }

        private SkillCheckRoll CreateSkillCheckRoll(NetworkDiceRollType diceRollType, RuleSkillCheck ruleSkillCheck)
        {
            var roll = new SkillCheckRoll(ruleSkillCheck.Initiator.UniqueId, ruleSkillCheck.GetType().Name, diceRollType, ruleSkillCheck.StatValue)
            {
                EnsureSuccess = ruleSkillCheck.EnsureSuccess,
                DifficultyCheck = ruleSkillCheck.DC,
                RequireSuccessBonus = ruleSkillCheck.RequiresSuccessBonus,
                Take10ForSuccess = ruleSkillCheck.Take10ForSuccess,
                StatType = ruleSkillCheck.StatType.ToString(),
                SourceEntityId = ruleSkillCheck.Reason?.SourceEntity?.UniqueId
            };

            return roll;
        }

        private SpellResistanceCheckRoll CreateSpellResistanceCheckRoll(NetworkDiceRollType diceRollType, RuleSpellResistanceCheck ruleSpellResistanceCheck)
        {
            var roll = new SpellResistanceCheckRoll(ruleSpellResistanceCheck.Initiator.UniqueId, ruleSpellResistanceCheck.GetType().Name, diceRollType, ruleSpellResistanceCheck.TotalBonusValue)
            {
                SpellPenetration = ruleSpellResistanceCheck.SpellPenetration,
                SpellResistance = ruleSpellResistanceCheck.SpellResistance,
                SchoolType = ruleSpellResistanceCheck.Ability.School.ToString(),
                AbilityType = ruleSpellResistanceCheck.Ability.Type.ToString(),
                TargetId = ruleSpellResistanceCheck.Target.UniqueId,
                AbilityName = ruleSpellResistanceCheck.Ability.name,
                ActionType = ruleSpellResistanceCheck.Ability.ActionType.ToString()
            };

            return roll;
        }

        private HealDamageRoll CreateHealDamageRoll(NetworkDiceRollType diceRollType, RuleHealDamage ruleHealDamage)
        {
            var roll = new HealDamageRoll(ruleHealDamage.Initiator?.UniqueId, ruleHealDamage.GetType().Name, diceRollType, 0)
            {
                AbilityName = ruleHealDamage.Reason.Ability?.StickyTouch?.NameForAcronym ?? ruleHealDamage.Reason.Ability?.NameForAcronym,
                AbilitySchoolId = ruleHealDamage.Reason.Ability?.Spellbook?.Blueprint.name,
                TargetId = ruleHealDamage.Target?.UniqueId,
                EmpowerModifier = ruleHealDamage.EmpowerModifier,
                AdditionalBonus = ruleHealDamage.AdditionalBonus,
                HealResistance = ruleHealDamage.HealResistance
            };

            return roll;
        }

        private AbilityDamageRoll CreateAbilityUse(NetworkDiceRollType diceRollType, RuleCalculateDamage ruleCalculateDamage)
        {
            var roll = new AbilityDamageRoll(ruleCalculateDamage.Initiator.UniqueId, ruleCalculateDamage.ParentRule?.GetType().Name, diceRollType, ruleCalculateDamage.TotalBonusValue)
            {
                AbilityName = ruleCalculateDamage.Reason.Ability?.NameForAcronym,
                AbilitySchoolId = ruleCalculateDamage.Reason.Ability?.Spellbook?.Blueprint.name,
                TargetId = ruleCalculateDamage.Target?.UniqueId
            };

            return roll;
        }

        private UnspecifiedDamageRoll CreateUnspecifiedDamage(NetworkDiceRollType diceRollType, RuleCalculateDamage ruleCalculateDamage)
        {
            var roll = new UnspecifiedDamageRoll(ruleCalculateDamage.Initiator.UniqueId, ruleCalculateDamage.ParentRule?.GetType().Name, diceRollType, ruleCalculateDamage.TotalBonusValue)
            {
                TargetId = ruleCalculateDamage.Target?.UniqueId
            };

            return roll;
        }

        private SavingThrowRoll CreateSavingThrowRoll(NetworkDiceRollType diceRollType, RuleSavingThrow ruleSavingThrow)
        {
            var roll = new SavingThrowRoll(ruleSavingThrow.Initiator.UniqueId, ruleSavingThrow.GetType().Name, diceRollType, totalModifierBonus: 0)
            {
                StatType = ruleSavingThrow.StatType.ToString(),
                ReasonAbilityName = ruleSavingThrow.Reason?.Ability?.NameForAcronym,
                ReasonCasterId = ruleSavingThrow.Reason?.Caster?.UniqueId,
                DifficultyClass = ruleSavingThrow.DifficultyClass,
            };

            return roll;
        }

        private AttackRoll CreateAttackRoll(NetworkDiceRollType diceRollType, RuleAttackRoll ruleAttackRoll, bool isCriticalRoll)
        {
            var roll = new AttackRoll(ruleAttackRoll.Initiator.UniqueId, ruleAttackRoll.GetType().Name, diceRollType, 0) // ruleAttackRoll.AttackBonus is not consistent due to flanking3
            {
                AttackType = ruleAttackRoll.AttackType.ToString(),
                TargetId = ruleAttackRoll.Target.UniqueId,
                IsCriticalRoll = isCriticalRoll,
                FortificationChance = ruleAttackRoll.FortificationChance,
                MissChance = ruleAttackRoll.MissChanceValue,
                AttackWithWeapon = ruleAttackRoll.RuleAttackWithWeapon == null ? null : CreateAttackWithWeaponRoll(diceRollType, ruleAttackRoll.RuleAttackWithWeapon)
            };

            return roll;
        }

        private AttackWithWeaponRoll CreateAttackWithWeaponRoll(NetworkDiceRollType diceRollType, RuleAttackWithWeapon attackWithWeapon)
        {
            var roll = new AttackWithWeaponRoll(attackWithWeapon.Initiator.UniqueId, attackWithWeapon.GetType().Name, diceRollType, 0) //  attackWithWeapon.AttackRoll.AttackBonus
            {
                AttackNumber = attackWithWeapon.AttackNumber,
                IsAttackOfOpportunity = attackWithWeapon.IsAttackOfOpportunity,
                TargetId = attackWithWeapon.Target.UniqueId,
                ExtraAttack = attackWithWeapon.ExtraAttack,
                IsFirstAttack = attackWithWeapon.IsFirstAttack,
            };

            return roll;
        }

        private InitiativeRoll CreateInitiativeRoll(NetworkDiceRollType diceRollType, RuleInitiativeRoll initiativeRoll)
        {
            var roll = new InitiativeRoll(initiativeRoll.Initiator.UniqueId, initiativeRoll.GetType().Name, diceRollType, 0); // initiativeRoll.Modifier
            return roll;
        }

        private CastingDefensivelyRoll CreateCastingDefensivelyRoll(NetworkDiceRollType diceRollType, RuleCheckCastingDefensively ruleCheckCastingDefensively)
        {
            var roll = new CastingDefensivelyRoll(ruleCheckCastingDefensively.Initiator.UniqueId, ruleCheckCastingDefensively.GetType().Name, diceRollType, ruleCheckCastingDefensively.TotalBonusValue)
            {
                AbilityName = ruleCheckCastingDefensively.Reason.Ability?.StickyTouch?.NameForAcronym ?? ruleCheckCastingDefensively.Reason.Ability?.NameForAcronym ?? ruleCheckCastingDefensively.Spell?.NameForAcronym,
                Concentration = ruleCheckCastingDefensively.Concentration,
                DC = ruleCheckCastingDefensively.DC
            };

            return roll;
        }

        private CastSpellRoll CreateCastSpellRoll(NetworkDiceRollType diceRollType, RuleCastSpell ruleCastSpell, bool isSpellFailure)
        {
            var roll = new CastSpellRoll(ruleCastSpell.Initiator.UniqueId, ruleCastSpell.GetType().Name, diceRollType, 0)
            {
                ArcaneSpellFailureChance = ruleCastSpell.ArcaneSpellFailureChance,
                SpellFailureChance = ruleCastSpell.SpellFailureChance,
                IsSpellFailure = isSpellFailure,
                UseMagicDeviceType = ruleCastSpell.UseMagicDeviceCheck?.UseMagicDeviceType?.ToString()
            };

            return roll;
        }

        private EnterStealthRoll CreateEnterStealthRoll(NetworkDiceRollType diceRollType, RuleEnterStealth ruleEnterStealth)
        {
            var roll = new EnterStealthRoll(ruleEnterStealth.Initiator.UniqueId, ruleEnterStealth.GetType().Name, diceRollType, 0)
            {
                IsFullSpeed = ruleEnterStealth.FullSpeed,
                ResultOverride = ruleEnterStealth.D20.ResultOverride
            };

            return roll;
        }

        private ChanceRoll CreateChanceRoll(NetworkDiceRollType diceRollType, RuleRollChance ruleRollChance)
        {
            var roll = new ChanceRoll(ruleRollChance.Initiator.UniqueId, ruleRollChance.GetType().Name, diceRollType, 0)
            {
                Chance = ruleRollChance.Chance,
                Type = ruleRollChance.Type.ToString()
            };

            return roll;
        }

        private DealStatDamageRoll CreateDealStatDamageRoll(NetworkDiceRollType diceRollType, RuleDealStatDamage ruleDealStatDamage, int criticalModifier)
        {
            var roll = new DealStatDamageRoll(ruleDealStatDamage.Initiator.UniqueId, ruleDealStatDamage.GetType().Name, diceRollType, ruleDealStatDamage.Bonus)
            {
                DiceRolls = ruleDealStatDamage.Dices.Rolls,
                DiceFormulaType = ruleDealStatDamage.Dices.Dice.ToString(),
                HalfBecauseSavingThrow = ruleDealStatDamage.HalfBecauseSavingThrow,
                CriticalModifierName = ruleDealStatDamage.CriticalModifier?.ToString(),
                CriticalModifierValue = criticalModifier,
                Immune = ruleDealStatDamage.Immune,
                Maximize = ruleDealStatDamage.Maximize,
                IsDrain = ruleDealStatDamage.IsDrain,
                Empower = ruleDealStatDamage.Empower,
                MinStatScoreAfterDamage = ruleDealStatDamage.MinStatScoreAfterDamage
            };

            return roll;
        }

        private DrainEnergyRoll CreateDrainEnergyRoll(NetworkDiceRollType diceRollType, RuleDrainEnergy ruleDrainEnergy, RuleRollDice ruleRollDice)
        {
            var roll = new DrainEnergyRoll(ruleDrainEnergy.Initiator.UniqueId, ruleDrainEnergy.GetType().Name, diceRollType, 0)
            {
                DiceRolls = ruleRollDice.DiceFormula.Rolls,
                DiceFormulaType = ruleRollDice.DiceFormula.Dice.ToString(),
                CriticalModifierName = ruleDrainEnergy.CriticalModifier?.ToString(),
                TargetIsImmune = ruleDrainEnergy.TargetIsImmune,
                Empower = ruleDrainEnergy.Empower,
                DrainValue = ruleDrainEnergy.DrainValue,
            };

            return roll;
        }

        private CombatManeuverRoll CreateCombatManeuverRoll(NetworkDiceRollType diceRollType, RuleCombatManeuver ruleCombatManeuver)
        {
            var roll = new CombatManeuverRoll(ruleCombatManeuver.Initiator.UniqueId, ruleCombatManeuver.GetType().Name, diceRollType, 0)
            {
                TargetCMD = ruleCombatManeuver.TargetCMD,
                Type = ruleCombatManeuver.Type.ToString(),
                WeaponName = ruleCombatManeuver.AttackRule?.Weapon.NameForAcronym,
                TargetUnitId = ruleCombatManeuver.Target?.UniqueId,
                IncreasedDuration = ruleCombatManeuver.IncreasedDuration
            };

            return roll;
        }

        private int RollHealDamage(RuleHealDamage ruleHealDamage, DiceFormula diceFormula)
        {
            var healDamage = CreateHealDamageRoll(NetworkDiceRollType.Heal, ruleHealDamage);
            var deterministicRoll = RollDice(healDamage, diceFormula);
            return deterministicRoll;
        }

        private int? RollDamage(RuleCalculateDamage ruleCalculateDamage, DiceFormula diceFormula)
        {
            var dealDamage = GetDamageRoll(ruleCalculateDamage);
            if (dealDamage == null)
            {
                return null;
            }

            var deterministicRoll = RollDice(dealDamage, diceFormula);
            return deterministicRoll;
        }

        private RuleRollD100 RollDealStatDamage(RuleDealStatDamage ruleDealStatDamage, DiceFormula damageFormula, int criticalModifier)
        {
            var chance = CreateDealStatDamageRoll(NetworkDiceRollType.Damage, ruleDealStatDamage, criticalModifier);
            var deterministicRoll = RollDice(chance, damageFormula);

            var d100 = RuleRollD100.FromInt(ruleDealStatDamage.Initiator, deterministicRoll);
            d100.RollHistory = [deterministicRoll];
            return d100;
        }

        private int RollDice(NetworkDiceRollBase roll, DiceFormula diceFormula)
        {
            try
            {
                var rollIdentifier = roll.GetIdString();

                var seededContext = _multiplayerActorAccessor.Current.GetSeededContext();
                var identifier = $"{rollIdentifier}_{seededContext.Id}";
                var random = _valueGenerator.GetRandom(seededContext.Lifetime, identifier);
                var result = diceFormula.Roll(random);
                _logger.LogInformation("{RuleName} has been rolled deterministically. Type={Type}, UnitId={UnitId}, Result={Result}, Lifetime={Lifetime}, Identifier={Identifier}",
                    roll.RuleName, roll.RollType, roll.InitiatorId, result, seededContext.Lifetime, identifier);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while rolling dice deterministically");
                throw;
            }
        }
    }
}
