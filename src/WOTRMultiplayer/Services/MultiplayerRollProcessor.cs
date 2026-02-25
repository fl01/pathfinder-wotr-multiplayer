using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.GameModes;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.RuleSystem.Rules.Damage;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.GameInteraction.CombatLog;
using WOTRMultiplayer.Abstractions.Hashing;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.Entities.Rolls;
using WOTRMultiplayer.Entities.Rolls.Claiming.Values;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.Services.Random;

namespace WOTRMultiplayer.Services
{
    public class MultiplayerRollProcessor : IMultiplayerRollsProcessor
    {
        private readonly ILogger<MultiplayerRollProcessor> _logger;
        private readonly IGameInteractionService _gameInteractionService;
        private readonly ICombatInteractionService _combatInteractionService;
        private readonly IPlayerNotificationService _playerNotificationService;
        private readonly IDiceRollStorage _diceRollStorage;
        private readonly IHashService _hashService;
        private readonly IMultiplayerActorAccessor _multiplayerActorAccessor;
        private readonly IValueGenerator _valueGenerator;
        private readonly HashSet<string> _importantCutsceneAreas = new([
            "EstrodTower" // - using columns to damage enemies
            ], StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<WellKnownDiceFormulaKind, WellKnownDiceFormula> _wellKnownDiceFormulas = new()
        {
            { WellKnownDiceFormulaKind.RuleCheckCastingDefensively, new WellKnownDiceFormula(new DiceFormula(1, DiceType.D20), 0) },
            { WellKnownDiceFormulaKind.RuleInitiativeRoll, new WellKnownDiceFormula(new DiceFormula(1, DiceType.D20),  0) },
            { WellKnownDiceFormulaKind.RuleSpellResistanceCheck, new WellKnownDiceFormula(new DiceFormula(1, DiceType.D20),  0) },
            { WellKnownDiceFormulaKind.RuleAttackRoll, new WellKnownDiceFormula(new DiceFormula(1, DiceType.D20), 0) },
            { WellKnownDiceFormulaKind.RuleCheckConcentration, new WellKnownDiceFormula(new DiceFormula(1, DiceType.D20), 0) },
            { WellKnownDiceFormulaKind.RuleSkillCheck, new WellKnownDiceFormula(new DiceFormula(1, DiceType.D20), 0) },
            { WellKnownDiceFormulaKind.RuleDispelMagic, new WellKnownDiceFormula(new DiceFormula(1, DiceType.D20), 0) },

            { WellKnownDiceFormulaKind.SpellFailureRoll, new WellKnownDiceFormula(new DiceFormula(1, DiceType.D100), 0) },
            { WellKnownDiceFormulaKind.ArcaneSpellFailureRoll, new WellKnownDiceFormula(new DiceFormula(1, DiceType.D100), 0) },

            { WellKnownDiceFormulaKind.AttackParryData, new WellKnownDiceFormula(new DiceFormula(1, DiceType.D20), 0) },

            { WellKnownDiceFormulaKind.CriticalAttackRoll, new WellKnownDiceFormula(new DiceFormula(1, DiceType.D20), 0) },
            { WellKnownDiceFormulaKind.FortificationAttackRoll, new WellKnownDiceFormula(new DiceFormula(1, DiceType.D100), 0) },
        };

        public MultiplayerRollProcessor(
            ILogger<MultiplayerRollProcessor> logger,
            IGameInteractionService gameInteractionService,
            ICombatInteractionService combatInteractionService,
            IPlayerNotificationService playerNotificationService,
            IDiceRollStorage diceRollStorage,
            IHashService hashService,
            IMultiplayerActorAccessor multiplayerActorAccessor,
            IValueGenerator valueGenerator)
        {
            _logger = logger;
            _gameInteractionService = gameInteractionService;
            _combatInteractionService = combatInteractionService;
            _playerNotificationService = playerNotificationService;
            _diceRollStorage = diceRollStorage;
            _hashService = hashService;
            _multiplayerActorAccessor = multiplayerActorAccessor;
            _valueGenerator = valueGenerator;
        }

        public int? OnBeforeRuleCalculateDamageRoll(RuleCalculateDamage ruleCalculateDamage, DiceFormula diceFormula)
        {
            try
            {
                if (!IsRolledDeterministically(ruleCalculateDamage) || diceFormula.Rolls == 0 && diceFormula.Dice == DiceType.Zero)
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

        public bool OnBeforeRuleCalculateDamageBundle(RuleCalculateDamage ruleCalculateDamage)
        {
            try
            {
                if (!ShouldRetrieveRoll(ruleCalculateDamage) || IsRolledDeterministically(ruleCalculateDamage))
                {
                    return true;
                }

                var rollId = GetDamageRollId(ruleCalculateDamage);
                if (rollId == null)
                {
                    if (_combatInteractionService.IsInCrusadeTacticalCombat())
                    {
                        return true;
                    }

                    _logger.LogWarning("Damage Roll retrieving has been skipped due to unability to generate rollId. InitiatorName={InitiatorName}, InitiatorId={InitiatorId}", ruleCalculateDamage.Initiator?.CharacterName, ruleCalculateDamage.Initiator?.UniqueId);
                    return true;
                }

                var networkRoll = _multiplayerActorAccessor.Current.RetrieveRoll<NetworkDamageListRollValue>(rollId.Value, nameof(RuleCalculateDamage), ruleCalculateDamage.Initiator.UniqueId);

                if (networkRoll == null)
                {
                    _logger.LogCritical("Failed to acquire damage roll from remote player which guarantees desync in the game. RollId={RollId}", rollId.Value);

                    _playerNotificationService.AddCombatText(WellKnownKeys.GameNotifications.Rolls.MissingDamageRoll.Key, CombatTextSeverity.Critical, new UnitEntityLog(ruleCalculateDamage.Initiator.UniqueId));
                    return true;
                }
                var bundles = ruleCalculateDamage.DamageBundle.ToList();
                if (networkRoll.Value.Count != bundles.Count)
                {
                    _logger.LogCritical("Network damage contains invalid number of damage values. RollId={RollId}, ExpectedCount={ExpectedCount}, ActualCount={ActualCount}, Bundles={Bundles}",
                        rollId.Value, bundles.Count, networkRoll.Value.Count, bundles.Select(x => x.SourceFact?.NameForAcronym));

                    _playerNotificationService.AddCombatText(WellKnownKeys.GameNotifications.Rolls.DiscrepantDamageRoll.Key, CombatTextSeverity.Critical, new UnitEntityLog(ruleCalculateDamage.Initiator.UniqueId), bundles.Count, networkRoll.Value.Count);

                    return true;
                }

                for (int i = 0; i < bundles.Count; i++)
                {
                    var damage = bundles[i];
                    var networkDamageValue = networkRoll.Value[i];
                    var damageValue = new DamageValue(damage, networkDamageValue.ValueWithoutReduction, networkDamageValue.RollAndBonusValue, networkDamageValue.RollResult, networkDamageValue.TacticalCombatDRModifier);
                    damageValue.Source.MaximumValue = networkDamageValue.MaximumDamage;
                    ruleCalculateDamage.CalculatedDamage.Add(damageValue);
                }

                _logger.LogInformation("Damage roll result has been acquired from another player. RollId={RollId}, DamageValuesCount={DamageValuesCount}", rollId.Value, ruleCalculateDamage.CalculatedDamage.Count);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error before damage bundle rule trigger");
                throw;
            }
        }

        public void OnAfterRuleCalculateDamageBundle(RuleCalculateDamage ruleCalculateDamage)
        {
            try
            {
                if (!ShouldStoreRoll(ruleCalculateDamage) || IsRolledDeterministically(ruleCalculateDamage))
                {
                    return;
                }

                var rollId = GetDamageRollId(ruleCalculateDamage);
                if (rollId == null)
                {
                    if (_combatInteractionService.IsInCrusadeTacticalCombat())
                    {
                        return;
                    }

                    _logger.LogWarning("Damage Roll saving has been skipped due to unability to generate rollId. InitiatorName={InitiatorName}, InitiatorId={InitiatorId}", ruleCalculateDamage.Initiator?.CharacterName, ruleCalculateDamage.Initiator?.UniqueId);
                    return;
                }

                var rollValue = new NetworkDamageListRollValue
                {
                    Value = [..ruleCalculateDamage.CalculatedDamage.Select(x => new NetworkDamageRollValue
                    {
                        MaximumDamage = x.Source.MaximumValue,
                        RollAndBonusValue = x.RollAndBonusValue,
                        RollResult = x.RollResult,
                        TacticalCombatDRModifier = x.TacticalCombatDRModifier,
                        ValueWithoutReduction = x.ValueWithoutReduction
                    })]
                };

                SaveRollValue(rollId.Value, rollValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error after damage rule trigger");
                throw;
            }
        }

        public bool OnBeforeRollRuleHealDamage(RuleHealDamage ruleHealDamage, bool isTacticalCombat, DiceFormula diceFormula)
        {
            try
            {
                var isRolledDeterministically = IsRolledDeterministically(ruleHealDamage);
                if (!ShouldRetrieveRoll(ruleHealDamage) && !isRolledDeterministically || diceFormula.Rolls == 0 && diceFormula.Dice == DiceType.Zero)
                {
                    return true;
                }

                var result = isRolledDeterministically ? RollHealDamage(ruleHealDamage, isTacticalCombat, diceFormula) : RetrieveHealDamageRoll(ruleHealDamage, isTacticalCombat);
                if (result == null)
                {
                    return true;
                }

                ruleHealDamage.RollResult = result.Value;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error before heal rule trigger");
                throw;
            }
        }

        public void OnAfterRollRuleHealDamage(RuleHealDamage ruleHealDamage, int result, bool isTacticalCombat)
        {
            try
            {
                if (!ShouldStoreRoll(ruleHealDamage) || IsRolledDeterministically(ruleHealDamage) || ruleHealDamage.HealFormula.ModifiedValue.Rolls == 0 && ruleHealDamage.HealFormula.ModifiedValue.Dice == DiceType.Zero)
                {
                    return;
                }

                var roll = CreateHealDamageRoll(NetworkDiceRollType.Hit, ruleHealDamage, isTacticalCombat);
                var rollId = GetDiceRollId(roll);
                if (rollId == null)
                {
                    _logger.LogWarning("Heal Damage saving has been skipped due to unability to generate rollId. InitiatorName={InitiatorName}, InitiatorId={InitiatorId}", ruleHealDamage.Initiator?.CharacterName, ruleHealDamage.Initiator?.UniqueId);
                    return;
                }

                var rollValue = new NetworkIntRollValue
                {
                    RollHistory = [],
                    Value = result
                };

                SaveRollValue(rollId.Value, rollValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error after heal rule trigger");
                throw;
            }
        }

        public bool OnBeforeRuleAttackOvercomeConcealmentRoll(RuleAttackRoll ruleAttackRoll)
        {
            try
            {
                var isRolledDeterministically = IsRolledDeterministically(ruleAttackRoll, RuleAttackRollSubType.Concealment);
                if (!ShouldRetrieveRoll(ruleAttackRoll) && !isRolledDeterministically)
                {
                    return true;
                }

                var d100 = isRolledDeterministically ? RollAttackOvercomeConcealment(ruleAttackRoll) : RetrieveAttackOvercomeConcealment(ruleAttackRoll);
                if (d100 == null)
                {
                    return true;
                }

                ruleAttackRoll.MissChanceRoll = d100;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public void OnAfterRuleAttackOvercomeConcealmentRoll(RuleAttackRoll ruleAttackRoll)
        {
            try
            {
                if (!ShouldStoreRoll(ruleAttackRoll) || IsRolledDeterministically(ruleAttackRoll, RuleAttackRollSubType.Concealment) || ruleAttackRoll.MissChanceRoll == null)
                {
                    return;
                }

                var roll = CreateAttackOvercomeConcealmentRoll(NetworkDiceRollType.Hit, ruleAttackRoll);
                SaveIntRollValue(roll, ruleAttackRoll.MissChanceRoll);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public bool OnBeforeRuleAttackFortificationRoll(RuleAttackRoll ruleAttackRoll)
        {
            try
            {
                var isRolledDeterministically = IsRolledDeterministically(ruleAttackRoll, RuleAttackRollSubType.Fortification);
                if (!ShouldRetrieveRoll(ruleAttackRoll) && !isRolledDeterministically)
                {
                    return true;
                }

                var d100 = isRolledDeterministically ? RollFortificationAttackRoll(ruleAttackRoll) : RetrieveFortificationAttackRoll(ruleAttackRoll);
                if (d100 == null)
                {
                    return true;
                }

                ruleAttackRoll.FortificationRoll = d100;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public bool OnBeforeRuleAttackRoll(RuleAttackRoll ruleAttackRoll)
        {
            try
            {
                if (ruleAttackRoll.IsCriticalRoll)
                {
                    var isCriticalRolledDeterministically = IsRolledDeterministically(ruleAttackRoll, RuleAttackRollSubType.Critical);
                    if (!ShouldRetrieveRoll(ruleAttackRoll) && !isCriticalRolledDeterministically)
                    {
                        return true;
                    }

                    var criticalD20 = isCriticalRolledDeterministically ? RollCriticalAttackRoll(ruleAttackRoll) : RetrieveCriticalAttackRoll(ruleAttackRoll);
                    if (criticalD20 == null)
                    {
                        return true;
                    }
                    ruleAttackRoll.CriticalConfirmationD20 = criticalD20;
                    return false;
                }

                var isRolledDeterministically = IsRolledDeterministically(ruleAttackRoll, RuleAttackRollSubType.None);
                if (!ShouldRetrieveRoll(ruleAttackRoll) && !isRolledDeterministically)
                {
                    return true;
                }

                var d20 = isRolledDeterministically ? RollAttackRoll(ruleAttackRoll) : RetrieveAttackRoll(ruleAttackRoll);
                if (d20 == null)
                {
                    return true;
                }
                ruleAttackRoll.D20 = d20;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public void OnAfterRuleAttackRollTrigger(RuleAttackRoll ruleAttackRoll)
        {
            try
            {
                if (!ShouldStoreRoll(ruleAttackRoll) || ruleAttackRoll.D20 == null)
                {
                    return;
                }

                if (!IsRolledDeterministically(ruleAttackRoll, RuleAttackRollSubType.None))
                {
                    var roll = CreateAttackRoll(NetworkDiceRollType.Hit, ruleAttackRoll, isCriticalRoll: false);
                    SaveIntRollValue(roll, ruleAttackRoll.D20);
                }

                if (ruleAttackRoll.IsCriticalRoll && !IsRolledDeterministically(ruleAttackRoll, RuleAttackRollSubType.Critical))
                {
                    var criticalRoll = CreateAttackRoll(NetworkDiceRollType.Hit, ruleAttackRoll, isCriticalRoll: true);
                    SaveIntRollValue(criticalRoll, ruleAttackRoll.CriticalConfirmationD20);
                }

                if (ruleAttackRoll.FortificationRoll != null && !IsRolledDeterministically(ruleAttackRoll, RuleAttackRollSubType.Fortification))
                {
                    var fortificationRoll = CreateFortificationAttackRoll(NetworkDiceRollType.Hit, ruleAttackRoll);
                    SaveIntRollValue(fortificationRoll, ruleAttackRoll.FortificationRoll);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public void OnAfterRuleSavingThrowTrigger(RuleSavingThrow ruleSavingThrow)
        {
            try
            {
                if (!ShouldStoreRoll(ruleSavingThrow) || IsRolledDeterministically(ruleSavingThrow))
                {
                    return;
                }

                var savingThrow = CreateSavingThrowRoll(NetworkDiceRollType.Hit, ruleSavingThrow);
                SaveIntRollValue(savingThrow, ruleSavingThrow.D20);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public bool OnBeforeRuleSavingThrowRoll(RuleSavingThrow ruleSavingThrow)
        {
            try
            {
                if (!ShouldRetrieveRoll(ruleSavingThrow) && !IsRolledDeterministically(ruleSavingThrow))
                {
                    return true;
                }

                var d20 = IsRolledDeterministically(ruleSavingThrow) ? RollSavingThrow(ruleSavingThrow) : RetrieveSavingThrow(ruleSavingThrow);
                if (d20 == null)
                {
                    return true;
                }

                ruleSavingThrow.D20 = d20;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public bool OnBeforeRuleSpellResistanceCheckRoll(RuleSpellResistanceCheck ruleSpellResistanceCheck)
        {
            try
            {
                var isRolledDeterministically = IsRolledDeterministically(ruleSpellResistanceCheck);
                if (!ShouldRetrieveRoll(ruleSpellResistanceCheck) && !isRolledDeterministically)
                {
                    return true;
                }

                var d20 = isRolledDeterministically ? RollSpellResistance(ruleSpellResistanceCheck) : RetrieveSpellResistance(ruleSpellResistanceCheck);
                if (d20 == null)
                {
                    return true;
                }

                ruleSpellResistanceCheck.Roll = d20;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public void OnAfterRuleSpellResistanceCheckTrigger(RuleSpellResistanceCheck ruleSpellResistanceCheck)
        {
            try
            {
                if (!ShouldStoreRoll(ruleSpellResistanceCheck) || IsRolledDeterministically(ruleSpellResistanceCheck) || ruleSpellResistanceCheck.Roll == null)
                {
                    return;
                }

                var roll = CreateSpellResistanceCheckRoll(NetworkDiceRollType.Hit, ruleSpellResistanceCheck);
                SaveIntRollValue(roll, ruleSpellResistanceCheck.Roll);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public bool OnBeforeRuleCheckConcentrationRoll(RuleCheckConcentration ruleCheckConcentration)
        {
            try
            {
                var isRolledDeterministically = IsRolledDeterministically(ruleCheckConcentration);
                if (!ShouldRetrieveRoll(ruleCheckConcentration) && !isRolledDeterministically)
                {
                    return true;
                }

                var d20 = isRolledDeterministically ? RollCheckConcentration(ruleCheckConcentration) : RetrieveCheckConcentration(ruleCheckConcentration);

                ruleCheckConcentration.ResultRollRaw = d20;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public void OnAfterRuleCheckConcentrationTrigger(RuleCheckConcentration ruleCheckConcentration)
        {
            try
            {
                if (!ShouldStoreRoll(ruleCheckConcentration) || IsRolledDeterministically(ruleCheckConcentration))
                {
                    return;
                }

                var roll = CreateConcentrationRoll(NetworkDiceRollType.Hit, ruleCheckConcentration);
                SaveIntRollValue(roll, ruleCheckConcentration.ResultRollRaw);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public bool OnBeforeRuleSkillCheckRoll(RuleSkillCheck ruleSkillCheck)
        {
            try
            {
                var isRolledDeterministically = IsRolledDeterministically(ruleSkillCheck);
                if (!ShouldRetrieveRoll(ruleSkillCheck) && !isRolledDeterministically)
                {
                    return true;
                }

                var d20 = isRolledDeterministically ? RollSkillCheck(ruleSkillCheck) : RetrieveSkillCheck(ruleSkillCheck);
                if (d20 == null)
                {
                    return true;
                }

                ruleSkillCheck.D20 = d20;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}. StackTrace={StackTrace}", MethodBase.GetCurrentMethod().Name, Environment.StackTrace);
                throw;
            }
        }

        public void OnAfterRuleSkillCheckTrigger(RuleSkillCheck ruleSkillCheck)
        {
            try
            {
                if (!ShouldStoreRoll(ruleSkillCheck) || IsRolledDeterministically(ruleSkillCheck))
                {
                    return;
                }

                var roll = CreateSkillCheckRoll(NetworkDiceRollType.Hit, ruleSkillCheck);
                SaveIntRollValue(roll, ruleSkillCheck.D20);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public bool OnBeforeRuleInitiativeRoll(RuleInitiativeRoll ruleInitiativeRoll)
        {
            try
            {
                var isRolledDeterministically = IsRolledDeterministically(ruleInitiativeRoll);
                if (!ShouldRetrieveRoll(ruleInitiativeRoll) && !isRolledDeterministically)
                {
                    return true;
                }

                var d20 = isRolledDeterministically ? RollInitiative(ruleInitiativeRoll) : RetrieveInitiativeRoll(ruleInitiativeRoll);
                if (d20 == null)
                {
                    return true;
                }

                ruleInitiativeRoll.D20 = d20;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public void OnAfterRuleInitiativeRollTrigger(RuleInitiativeRoll ruleInitiativeRoll)
        {
            try
            {
                if (!ShouldStoreRoll(ruleInitiativeRoll) || IsRolledDeterministically(ruleInitiativeRoll))
                {
                    return;
                }

                var roll = CreateInitiativeRoll(NetworkDiceRollType.Hit, ruleInitiativeRoll);
                SaveIntRollValue(roll, ruleInitiativeRoll.D20);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public bool OnBeforeRuleConcealmentCheckTrigger(RuleConcealmentCheck ruleConcealmentCheck)
        {
            try
            {
                var isRolledDeterministically = IsRolledDeterministically(ruleConcealmentCheck);
                if (!ShouldRetrieveRoll(ruleConcealmentCheck) && !isRolledDeterministically)
                {
                    return true;
                }

                var d100 = isRolledDeterministically ? RollConcealmentCheck(ruleConcealmentCheck) : RetrieveConcealmentCheck(ruleConcealmentCheck);
                if (d100 == null)
                {
                    return true;
                }

                ruleConcealmentCheck.Roll.m_Result = d100;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public void OnAfterRuleConcealmentCheckTrigger(RuleConcealmentCheck ruleConcealmentCheck)
        {
            try
            {
                if (!ShouldStoreRoll(ruleConcealmentCheck) || IsRolledDeterministically(ruleConcealmentCheck) || ruleConcealmentCheck.ConcealmentValue <= 0)
                {
                    return;
                }

                var roll = CreateConcealmentRoll(NetworkDiceRollType.Hit, ruleConcealmentCheck);
                SaveIntRollValue(roll, ruleConcealmentCheck.Roll);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public bool OnBeforeParryDataTrigger(RuleAttackRoll.ParryData parryData)
        {
            try
            {
                var isRolledDeterministically = IsRolledDeterministically(parryData);
                if (!ShouldRetrieveRoll(parryData) && !isRolledDeterministically)
                {
                    return true;
                }

                var d20 = isRolledDeterministically ? RollAttackParryData(parryData) : RetrieveAttackParryData(parryData);
                if (d20 == null)
                {
                    return true;
                }

                parryData.Roll = d20;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public void OnAfterParryDataTrigger(RuleAttackRoll.ParryData parryData)
        {
            try
            {
                if (!ShouldStoreRoll(parryData) || IsRolledDeterministically(parryData))
                {
                    return;
                }

                var roll = CreateParryRoll(NetworkDiceRollType.Hit, parryData);
                SaveIntRollValue(roll, parryData.Roll);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public bool OnBeforeRuleDispelMagicRoll(RuleDispelMagic ruleDispelMagic)
        {
            try
            {
                var isRolledDeterministically = IsRolledDeterministically(ruleDispelMagic);
                if (!ShouldRetrieveRoll(ruleDispelMagic) && !isRolledDeterministically)
                {
                    return true;
                }

                var d20 = isRolledDeterministically ? RollDispelMagic(ruleDispelMagic) : RetrieveDispelMagic(ruleDispelMagic);
                if (d20 == null)
                {
                    return true;
                }

                ruleDispelMagic.CheckRoll = d20;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public void OnAfterRuleDispelMagicTrigger(RuleDispelMagic ruleDispelMagic)
        {
            try
            {
                if (!ShouldStoreRoll(ruleDispelMagic) || IsRolledDeterministically(ruleDispelMagic))
                {
                    return;
                }

                var roll = CreateDispelMagicRoll(NetworkDiceRollType.Hit, ruleDispelMagic);
                SaveIntRollValue(roll, ruleDispelMagic.CheckRoll);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public bool OnBeforeRuleEnterStealthRoll(RuleEnterStealth ruleEnterStealth)
        {
            try
            {
                var isRolledDeterministically = IsRolledDeterministically(ruleEnterStealth);
                if (!ShouldRetrieveRoll(ruleEnterStealth) && !isRolledDeterministically || ruleEnterStealth.D20.ResultOverride == 20)
                {
                    return true;
                }

                var d20 = isRolledDeterministically ? RollEnterStealth(ruleEnterStealth) : RetrieveEnterStealth(ruleEnterStealth);
                if (d20 == null)
                {
                    return true;
                }

                // need to preserve original D20.ResultOverride value (UnitStealthController.TickUnit)
                ruleEnterStealth.D20.m_Result = d20;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public void OnAfterRuleEnterStealthTrigger(RuleEnterStealth ruleEnterStealth)
        {
            try
            {
                if (!ShouldStoreRoll(ruleEnterStealth) || IsRolledDeterministically(ruleEnterStealth) || ruleEnterStealth.D20.ResultOverride == 20)
                {
                    return;
                }

                var roll = CreateEnterStealthRoll(NetworkDiceRollType.Hit, ruleEnterStealth);
                SaveIntRollValue(roll, ruleEnterStealth.D20);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public void OnBeforeRuleRollChanceTrigger(RuleRollChance ruleRollChance)
        {
            try
            {
                if (!ShouldRetrieveRoll(ruleRollChance))
                {
                    return;
                }

                var roll = CreateChanceRoll(NetworkDiceRollType.Hit, ruleRollChance);
                var d20 = RetrieveRoll<RuleRollD20>(roll, ruleRollChance.Initiator);
                if (d20 == null)
                {
                    return;
                }

                ruleRollChance.m_Result = d20;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public void OnAfterRuleRollChanceTrigger(RuleRollChance ruleRollChance)
        {
            try
            {
                if (!ShouldStoreRoll(ruleRollChance))
                {
                    return;
                }

                var roll = CreateChanceRoll(NetworkDiceRollType.Hit, ruleRollChance);
                SaveIntRollValue(roll, ruleRollChance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public bool OnBeforeRuleDrainEnergyRoll(RuleDrainEnergy ruleDrainEnergy, RuleRollDice ruleRollDice)
        {
            try
            {
                if (!ShouldRetrieveRoll(ruleDrainEnergy) || ruleRollDice.DiceFormula.Rolls == 0 && ruleRollDice.DiceFormula.Dice == DiceType.Zero)
                {
                    return true;
                }

                var roll = CreateDrainEnergyRoll(NetworkDiceRollType.Damage, ruleDrainEnergy, ruleRollDice);
                var d100 = RetrieveRoll<RuleRollD100>(roll, ruleDrainEnergy.Initiator);
                if (d100 == null)
                {
                    return true;
                }

                ruleRollDice.RollHistory = d100.RollHistory;
                ruleRollDice.m_Result = d100;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public void OnAfterRuleDrainEnergyRoll(RuleDrainEnergy ruleDrainEnergy, RuleRollDice ruleRollDice)
        {
            try
            {
                if (!ShouldStoreRoll(ruleDrainEnergy) || ruleRollDice.DiceFormula.Rolls == 0 && ruleRollDice.DiceFormula.Dice == DiceType.Zero)
                {
                    return;
                }

                var roll = CreateDrainEnergyRoll(NetworkDiceRollType.Damage, ruleDrainEnergy, ruleRollDice);
                SaveIntRollValue(roll, ruleRollDice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public bool OnBeforeRuleCombatManeuverRoll(RuleCombatManeuver ruleCombatManeuver)
        {
            try
            {
                if (!ShouldRetrieveRoll(ruleCombatManeuver))
                {
                    return true;
                }

                var roll = CreateCombatManeuverRoll(NetworkDiceRollType.Hit, ruleCombatManeuver);
                var d20 = RetrieveRoll<RuleRollD20>(roll, ruleCombatManeuver.Initiator);
                if (d20 == null)
                {
                    return true;
                }

                ruleCombatManeuver.InitiatorRoll = d20;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public void OnAfterRuleCombatManeuverRoll(RuleCombatManeuver ruleCombatManeuver, RuleRollD20 rollD20)
        {
            try
            {
                if (!ShouldStoreRoll(ruleCombatManeuver))
                {
                    return;
                }

                var roll = CreateCombatManeuverRoll(NetworkDiceRollType.Hit, ruleCombatManeuver);
                SaveIntRollValue(roll, rollD20);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public int? OnBeforeRuleDealStatDamageRoll(RuleDealStatDamage ruleDealStatDamage, int criticalModifier)
        {
            try
            {
                if (!ShouldRetrieveRoll(ruleDealStatDamage))
                {
                    return null;
                }

                var roll = CreateDealStatDamageRoll(NetworkDiceRollType.Damage, ruleDealStatDamage, criticalModifier);
                var d100 = RetrieveRoll<RuleRollD100>(roll, ruleDealStatDamage.Initiator);
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

        public void OnAfterRuleDealStatDamageRoll(RuleDealStatDamage ruleDealStatDamage, RuleRollD100 damageRoll, int criticalModifier)
        {
            try
            {
                if (!ShouldStoreRoll(ruleDealStatDamage))
                {
                    return;
                }

                var statDamageRoll = CreateDealStatDamageRoll(NetworkDiceRollType.Damage, ruleDealStatDamage, criticalModifier);
                SaveIntRollValue(statDamageRoll, damageRoll);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public bool OnBeforeRuleCastSpellRoll(RuleCastSpell ruleCastSpell, bool isSpellFailure)
        {
            try
            {
                var isRolledDeterministically = IsRolledDeterministically(ruleCastSpell);
                if (!ShouldRetrieveRoll(ruleCastSpell) && !isRolledDeterministically)
                {
                    return true;
                }

                var d100 = isRolledDeterministically ?
                    isSpellFailure ? RollSpellFailureRoll(ruleCastSpell)
                        : RollArcaneSpellFailureRoll(ruleCastSpell)
                    : RetrieveCastSpellRoll(ruleCastSpell, isSpellFailure);

                if (d100 == null)
                {
                    return true;
                }

                if (isSpellFailure)
                {
                    ruleCastSpell.SpellFailureRoll = d100;
                }
                else
                {
                    ruleCastSpell.ArcaneSpellFailureRoll = d100;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public void OnAfterRuleCastSpellTrigger(RuleCastSpell ruleCastSpell)
        {
            try
            {
                if (!ShouldStoreRoll(ruleCastSpell) || IsRolledDeterministically(ruleCastSpell))
                {
                    return;
                }

                if (ruleCastSpell.SpellFailureRoll != null)
                {
                    var roll = CreateCastSpellRoll(NetworkDiceRollType.Hit, ruleCastSpell, true);
                    SaveIntRollValue(roll, ruleCastSpell.SpellFailureRoll);
                }

                if (ruleCastSpell.ArcaneSpellFailureRoll != null)
                {
                    var roll = CreateCastSpellRoll(NetworkDiceRollType.Hit, ruleCastSpell, false);
                    SaveIntRollValue(roll, ruleCastSpell.ArcaneSpellFailureRoll);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public bool OnBeforeRuleCheckCastingDefensivelyRoll(RuleCheckCastingDefensively ruleCheckCastingDefensively)
        {
            try
            {
                var isRolledDeterministically = IsRolledDeterministically(ruleCheckCastingDefensively);
                if (!ShouldRetrieveRoll(ruleCheckCastingDefensively) && !isRolledDeterministically)
                {
                    return true;
                }

                var d20 = isRolledDeterministically ? RollCastingDefensively(ruleCheckCastingDefensively) : RetrieveCheckCastingDefensivelyRoll(ruleCheckCastingDefensively);
                if (d20 == null)
                {
                    return true;
                }

                ruleCheckCastingDefensively.D20 = d20;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public void OnAfterRuleCheckCastingDefensivelyTrigger(RuleCheckCastingDefensively ruleCheckCastingDefensively)
        {
            try
            {
                if ((!ShouldStoreRoll(ruleCheckCastingDefensively) && !IsRolledDeterministically(ruleCheckCastingDefensively)) || ruleCheckCastingDefensively.AutoFail)
                {
                    return;
                }

                var roll = CreateCastingDefensivelyRoll(NetworkDiceRollType.Hit, ruleCheckCastingDefensively);
                SaveIntRollValue(roll, ruleCheckCastingDefensively.D20);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {MethodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        private bool ShouldRetrieveRoll(object rule)
        {
            return _multiplayerActorAccessor.Current != null && IsMeaningfulRoll(rule) && !IsRollOwner(rule);
        }

        private bool ShouldStoreRoll(object rule)
        {
            return _multiplayerActorAccessor.Current != null && IsMeaningfulRoll(rule) && IsRollOwner(rule);
        }

        private bool IsRolledDeterministically(object rule)
        {
            var currentArea = _multiplayerActorAccessor.Current?.CurrentArea;

            switch (rule)
            {
                case RuleSavingThrow:
                case RuleSkillCheck:
                    var rulebookEvent = (RulebookEvent)rule;
                    var IsGlobalMapOrMissingInitiator = currentArea != null && currentArea.IsGlobalMap
                        || _combatInteractionService.IsInCombat() && _gameInteractionService.IsDeadOrMissing(rulebookEvent.Initiator.UniqueId);
                    return IsGlobalMapOrMissingInitiator;
                case RuleHealDamage:
                case RuleSpellResistanceCheck:
                case RuleCheckCastingDefensively:
                case RuleInitiativeRoll:
                case RuleConcealmentCheck:
                case RuleCheckConcentration:
                case RuleEnterStealth:
                case RuleCastSpell:
                case RuleDispelMagic:
                case RuleAttackRoll.ParryData:
                case RuleCalculateDamage:
                    return true;
                default:
                    return false;
            }
        }

        private bool IsRolledDeterministically(RuleAttackRoll ruleAttackRoll, RuleAttackRollSubType subType)
        {
            var isRolled = subType switch
            {
                RuleAttackRollSubType.Concealment => true,
                RuleAttackRollSubType.Fortification => true,
                RuleAttackRollSubType.Critical => true,

                // default attack roll
                RuleAttackRollSubType.None => false,
                _ => false,
            };

            return isRolled;
        }

        private bool IsRollOwner(object rule)
        {
            return rule switch
            {
                RuleSkillCheck => _multiplayerActorAccessor.Host.IsActive,
                _ => _multiplayerActorAccessor.Current.IsDiceRollOwner(),
            };
        }

        private bool IsMeaningfulRoll(object rule)
        {
            var gameMode = _gameInteractionService.CurrentGameMode;
            if (gameMode == GameModeType.Dialog)
            {
                return rule is RuleSkillCheck or RuleSavingThrow or RuleCalculateDamage;
            }

            if (gameMode == GameModeType.Cutscene || gameMode == GameModeType.CutsceneGlobalMap)
            {
                // EstrodTower - using columns to damage enemies
                var areaName = _multiplayerActorAccessor.Current.CurrentArea?.Name;
                return rule is RuleCalculateDamage && _importantCutsceneAreas.Contains(areaName);
            }

            if (!_combatInteractionService.IsInCrusadeTacticalCombat())
            {
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
                }

                return true;
            }

            // ignore damage ranges shown on hover
            return rule switch
            {
                RuleAttackRoll attackRoll => !attackRoll.IsFake,
                RuleCalculateDamage calculateDamage when calculateDamage.ParentRule is RuleDealDamage dealDamage => !dealDamage.IsFake,
                _ => true,
            };
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

        private int? GetDiceRollId(NetworkDiceRollBase roll)
        {
            if (roll == null)
            {
                return null;
            }
            var rawId = roll.GetIdString();
            var id = _hashService.Murmur3(rawId);
            _logger.LogInformation("RollId has been generated. RollId={rollId}, RollType={RollType}, RuleName={RuleName}, IdString={IdString}", id, roll.RollType, roll.RuleName, rawId);
            return id;
        }

        private void SaveIntRollValue(NetworkDiceRollBase networkDiceRoll, RuleRollDice ruleRollDice)
        {
            var rollType = networkDiceRoll.GetType().Name;
            var rollId = GetDiceRollId(networkDiceRoll);
            if (rollId == null)
            {
                _logger.LogWarning("Roll saving has been skipped due to unability to generate rollId. DiceType={DiceType}, RollType={RollType}, InitiatorId={InitiatorId}", ruleRollDice.GetType().Name, rollType, networkDiceRoll.InitiatorId);
                return;
            }

            var rollValue = new NetworkIntRollValue
            {
                RollHistory = [.. ruleRollDice.RollHistory ?? []],
                Value = ruleRollDice.m_Result
            };

            SaveRollValue(rollId.Value, rollValue);
        }

        private void SaveRollValue(int rollId, RollValueBase rollValue)
        {
            var claimingList = _multiplayerActorAccessor.Current.GetOtherPlayers().Select(i => i.Id).ToList();
            _diceRollStorage.Add(rollId, claimingList, rollValue);

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

        private int? GetDamageRollId(RuleCalculateDamage ruleCalculateDamage)
        {
            NetworkDiceRollBase roll = GetDamageRoll(ruleCalculateDamage);
            if (roll == null)
            {
                return null;
            }

            var rollId = GetDiceRollId(roll);
            if (rollId == null)
            {
                _logger.LogWarning("Unable to get damage roll id due to unability to generate rollId. InitiatorName={InitiatorName}, InitiatorId={InitiatorId}", ruleCalculateDamage.Initiator?.CharacterName, ruleCalculateDamage.Initiator?.UniqueId);
                return null;
            }

            return rollId.Value;
        }

        private T RetrieveRoll<T>(NetworkDiceRollBase networkDiceRoll, UnitEntityData initiator)
            where T : RuleRollDice
        {
            try
            {
                var rollType = networkDiceRoll.GetType().Name;
                var rollId = GetDiceRollId(networkDiceRoll);
                if (rollId == null)
                {
                    _logger.LogWarning("Unable to retrieve roll due to null rollId. RollType={RollType}, InitiatorId={InitiatorId}", rollType, initiator.UniqueId);
                    return null;
                }

                var roll = _multiplayerActorAccessor.Current.RetrieveRoll<NetworkIntRollValue>(rollId.Value, networkDiceRoll.RuleName, networkDiceRoll.InitiatorId);
                if (roll == null)
                {
                    _logger.LogCritical("Failed to acquire roll from remote player which guarantees desync in the game. RollId={RollId}, RollType={RollType}, InitiatorId={InitiatorId}", rollId.Value, rollType, initiator.UniqueId);
                    _playerNotificationService.AddCombatText(WellKnownKeys.GameNotifications.Rolls.MissingRoll.Key, CombatTextSeverity.Critical, networkDiceRoll.RuleName, new UnitEntityLog(networkDiceRoll.InitiatorId));
                    return null;
                }

                var diceType = typeof(T);
                T dice = diceType switch
                {
                    _ when diceType == typeof(RuleRollD20) => RuleRollD20.FromInt(initiator, roll.Value) as T,
                    _ when diceType == typeof(RuleRollD100) => RuleRollD100.FromInt(initiator, roll.Value) as T,
                    _ => null,
                };

                if (dice == null)
                {
                    _logger.LogError("Roll has been retrieved, but dicetype is not supported. DiceType={DiceType}, RollId={RollId}", diceType, rollId.Value);
                    return null;
                }

                dice.RollHistory = [.. roll.RollHistory];

                _logger.LogInformation("Roll has been acquired from another player. DiceType={DiceType}, RollId={RollId}, Result={Result}, RollType={RollType}, InitiatorId={InitiatorId}",
                    diceType, rollId.Value, dice.Result, rollType, initiator.UniqueId);

                return dice;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unable to handle {MethodBase.GetCurrentMethod().Name}");
                throw;
            }
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

        private HealDamageRoll CreateHealDamageRoll(NetworkDiceRollType diceRollType, RuleHealDamage ruleHealDamage, bool isTacticalCombat)
        {
            var roll = new HealDamageRoll(ruleHealDamage.Initiator?.UniqueId, ruleHealDamage.GetType().Name, diceRollType, 0)
            {
                AbilityName = ruleHealDamage.Reason.Ability?.StickyTouch?.NameForAcronym ?? ruleHealDamage.Reason.Ability?.NameForAcronym,
                AbilitySchoolId = ruleHealDamage.Reason.Ability?.Spellbook?.Blueprint.name,
                TargetId = ruleHealDamage.Target?.UniqueId,
                EmpowerModifier = ruleHealDamage.EmpowerModifier,
                IsTacticalCombat = isTacticalCombat,
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

        private AttackOvercomeConcealmentRoll CreateAttackOvercomeConcealmentRoll(NetworkDiceRollType diceRollType, RuleAttackRoll ruleAttackRoll)
        {
            var roll = new AttackOvercomeConcealmentRoll(ruleAttackRoll.Initiator.UniqueId, ruleAttackRoll.GetType().Name, diceRollType, ruleAttackRoll.AttackBonus)
            {
                MissChance = ruleAttackRoll.MissChanceValue,
                AttackRoll = CreateAttackRoll(diceRollType, ruleAttackRoll, ruleAttackRoll.IsCriticalRoll)
            };

            return roll;
        }

        private FortificationAttackRoll CreateFortificationAttackRoll(NetworkDiceRollType diceRollType, RuleAttackRoll ruleAttackRoll)
        {
            var roll = new FortificationAttackRoll(ruleAttackRoll.Initiator.UniqueId, ruleAttackRoll.GetType().Name, diceRollType, ruleAttackRoll.AttackBonus)
            {
                FortificationChance = ruleAttackRoll.FortificationChance,
                AttackRoll = CreateAttackRoll(diceRollType, ruleAttackRoll, ruleAttackRoll.IsCriticalRoll)
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
                AttackWithWeapon = ruleAttackRoll.RuleAttackWithWeapon == null ? null : CreateAttackWithWeaponRoll(diceRollType, ruleAttackRoll.RuleAttackWithWeapon)
            };

            return roll;
        }

        private AttackWithWeaponRoll CreateAttackWithWeaponRoll(NetworkDiceRollType diceRollType, RuleAttackWithWeapon attackWithWeapon)
        {
            var roll = new AttackWithWeaponRoll(attackWithWeapon.Initiator.UniqueId, attackWithWeapon.GetType().Name, diceRollType, attackWithWeapon.AttackRoll.AttackBonus)
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
            var roll = new InitiativeRoll(initiativeRoll.Initiator.UniqueId, initiativeRoll.GetType().Name, diceRollType, initiativeRoll.Modifier);
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
                IsSpellFailure = isSpellFailure
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

        private WellKnownDiceFormula GetDiceFormula(WellKnownDiceFormulaKind wellKnownDiceFormula)
        {
            if (!_wellKnownDiceFormulas.TryGetValue(wellKnownDiceFormula, out var formula))
            {
                _logger.LogError("Dice formula is missing. Kind={Kind}", wellKnownDiceFormula);
            }

            return formula;
        }

        private int RollHealDamage(RuleHealDamage ruleHealDamage, bool isTacticalCombat, DiceFormula diceFormula)
        {
            var healDamage = CreateHealDamageRoll(NetworkDiceRollType.Hit, ruleHealDamage, isTacticalCombat);
            var deterministicRoll = RollDice(healDamage, diceFormula, 0);
            return deterministicRoll.Result;
        }

        private int? RollDamage(RuleCalculateDamage ruleCalculateDamage, DiceFormula diceFormula)
        {
            var dealDamage = GetDamageRoll(ruleCalculateDamage);
            if (dealDamage == null)
            {
                return null;
            }

            var deterministicRoll = RollDice(dealDamage, diceFormula, 0);
            return deterministicRoll.Result;
        }

        private RuleRollD20 RollInitiative(RuleInitiativeRoll ruleInitiativeRoll)
        {
            var initiative = CreateInitiativeRoll(NetworkDiceRollType.Hit, ruleInitiativeRoll);
            var formula = GetDiceFormula(WellKnownDiceFormulaKind.RuleInitiativeRoll);
            if (formula == null)
            {
                return null;
            }

            var deterministicRoll = RollDice(initiative, formula.Formula, formula.Rerolls);

            var d20 = RuleRollD20.FromInt(ruleInitiativeRoll.Initiator, deterministicRoll.Result);
            d20.RollHistory = deterministicRoll.History;
            return d20;
        }

        private RuleRollD20 RollSpellResistance(RuleSpellResistanceCheck ruleSpellResistanceCheck)
        {
            var spellResistance = CreateSpellResistanceCheckRoll(NetworkDiceRollType.Hit, ruleSpellResistanceCheck);
            var formula = GetDiceFormula(WellKnownDiceFormulaKind.RuleSpellResistanceCheck);
            if (formula == null)
            {
                return null;
            }

            var deterministicRoll = RollDice(spellResistance, formula.Formula, formula.Rerolls);

            var d20 = RuleRollD20.FromInt(ruleSpellResistanceCheck.Initiator, deterministicRoll.Result);
            d20.RollHistory = deterministicRoll.History;
            return d20;
        }

        private RuleRollD20 RollCastingDefensively(RuleCheckCastingDefensively ruleCheckCastingDefensively)
        {
            var castingDefensively = CreateCastingDefensivelyRoll(NetworkDiceRollType.Hit, ruleCheckCastingDefensively);
            var formula = GetDiceFormula(WellKnownDiceFormulaKind.RuleCheckCastingDefensively);
            if (formula == null)
            {
                return null;
            }

            var deterministicRoll = RollDice(castingDefensively, formula.Formula, formula.Rerolls);
            var d20 = RuleRollD20.FromInt(ruleCheckCastingDefensively.Initiator, deterministicRoll.Result);
            d20.RollHistory = deterministicRoll.History;
            return d20;
        }

        private RuleRollD100 RollAttackOvercomeConcealment(RuleAttackRoll ruleAttackRoll)
        {
            var attackOvercomeConcealment = CreateAttackOvercomeConcealmentRoll(NetworkDiceRollType.Hit, ruleAttackRoll);
            var deterministicRoll = RollDice(attackOvercomeConcealment, ruleAttackRoll.MissChanceRoll);

            var d100 = RuleRollD100.FromInt(ruleAttackRoll.Initiator, deterministicRoll.Result);
            d100.RollHistory = deterministicRoll.History;
            return d100;
        }

        private RuleRollD100 RollFortificationAttackRoll(RuleAttackRoll ruleAttackRoll)
        {
            var fortificationAttackRoll = CreateFortificationAttackRoll(NetworkDiceRollType.Hit, ruleAttackRoll);
            var formula = GetDiceFormula(WellKnownDiceFormulaKind.FortificationAttackRoll);
            if (formula == null)
            {
                return null;
            }
            var deterministicRoll = RollDice(fortificationAttackRoll, formula.Formula, formula.Rerolls);

            var d100 = RuleRollD100.FromInt(ruleAttackRoll.Initiator, deterministicRoll.Result);
            d100.RollHistory = deterministicRoll.History;
            return d100;
        }

        private RuleRollD20 RollAttackRoll(RuleAttackRoll ruleAttackRoll)
        {
            var attackRoll = CreateAttackRoll(NetworkDiceRollType.Hit, ruleAttackRoll, isCriticalRoll: false);
            var formula = GetDiceFormula(WellKnownDiceFormulaKind.RuleAttackRoll);
            if (formula == null)
            {
                return null;
            }
            var deterministicRoll = RollDice(attackRoll, formula.Formula, formula.Rerolls);

            var d20 = RuleRollD20.FromInt(ruleAttackRoll.Initiator, deterministicRoll.Result);
            d20.RollHistory = deterministicRoll.History;
            return d20;
        }

        private RuleRollD20 RollCriticalAttackRoll(RuleAttackRoll ruleAttackRoll)
        {
            var fortificationAttackRoll = CreateAttackRoll(NetworkDiceRollType.Hit, ruleAttackRoll, isCriticalRoll: true);
            var formula = GetDiceFormula(WellKnownDiceFormulaKind.CriticalAttackRoll);
            if (formula == null)
            {
                return null;
            }
            var deterministicRoll = RollDice(fortificationAttackRoll, formula.Formula, formula.Rerolls);

            var d20 = RuleRollD20.FromInt(ruleAttackRoll.Initiator, deterministicRoll.Result);
            d20.RollHistory = deterministicRoll.History;
            return d20;
        }


        private RuleRollD100 RollSpellFailureRoll(RuleCastSpell ruleCastSpell)
        {
            var spellFailureRoll = CreateCastSpellRoll(NetworkDiceRollType.Hit, ruleCastSpell, isSpellFailure: true);
            var formula = GetDiceFormula(WellKnownDiceFormulaKind.SpellFailureRoll);
            if (formula == null)
            {
                return null;
            }
            var deterministicRoll = RollDice(spellFailureRoll, formula.Formula, formula.Rerolls);

            var d100 = RuleRollD100.FromInt(ruleCastSpell.Initiator, deterministicRoll.Result);
            d100.RollHistory = deterministicRoll.History;
            return d100;
        }

        private RuleRollD100 RollArcaneSpellFailureRoll(RuleCastSpell ruleCastSpell)
        {
            var spellFailureRoll = CreateCastSpellRoll(NetworkDiceRollType.Hit, ruleCastSpell, isSpellFailure: false);
            var formula = GetDiceFormula(WellKnownDiceFormulaKind.ArcaneSpellFailureRoll);
            if (formula == null)
            {
                return null;
            }
            var deterministicRoll = RollDice(spellFailureRoll, formula.Formula, formula.Rerolls);

            var d100 = RuleRollD100.FromInt(ruleCastSpell.Initiator, deterministicRoll.Result);
            d100.RollHistory = deterministicRoll.History;
            return d100;
        }

        private RuleRollD20 RollSavingThrow(RuleSavingThrow ruleSavingThrow)
        {
            var savingThrow = CreateSavingThrowRoll(NetworkDiceRollType.Hit, ruleSavingThrow);
            var deterministicRoll = RollDice(savingThrow, ruleSavingThrow.D20);

            var d20 = RuleRollD20.FromInt(ruleSavingThrow.Initiator, deterministicRoll.Result);
            d20.RollHistory = deterministicRoll.History;
            return d20;
        }

        private RuleRollD20 RollSkillCheck(RuleSkillCheck ruleSkillCheck)
        {
            var skillCheck = CreateSkillCheckRoll(NetworkDiceRollType.Hit, ruleSkillCheck);
            var formula = GetDiceFormula(WellKnownDiceFormulaKind.RuleSkillCheck);
            if (formula == null)
            {
                return null;
            }
            var deterministicRoll = RollDice(skillCheck, formula.Formula, formula.Rerolls);

            var d20 = RuleRollD20.FromInt(ruleSkillCheck.Initiator, deterministicRoll.Result);
            d20.RollHistory = deterministicRoll.History;
            return d20;
        }

        private RuleRollD20 RollDispelMagic(RuleDispelMagic ruleDispelMagic)
        {
            var dispelMagic = CreateDispelMagicRoll(NetworkDiceRollType.Hit, ruleDispelMagic);
            var formula = GetDiceFormula(WellKnownDiceFormulaKind.RuleDispelMagic);
            if (formula == null)
            {
                return null;
            }
            var deterministicRoll = RollDice(dispelMagic, formula.Formula, formula.Rerolls);

            var d20 = RuleRollD20.FromInt(ruleDispelMagic.Initiator, deterministicRoll.Result);
            d20.RollHistory = deterministicRoll.History;
            return d20;
        }

        private RuleRollD20 RollEnterStealth(RuleEnterStealth ruleEnterStealth)
        {
            var enterStealth = CreateEnterStealthRoll(NetworkDiceRollType.Hit, ruleEnterStealth);
            var deterministicRoll = RollDice(enterStealth, ruleEnterStealth.D20);

            var d20 = RuleRollD20.FromInt(ruleEnterStealth.Initiator, deterministicRoll.Result);
            d20.RollHistory = deterministicRoll.History;
            return d20;
        }

        private RuleRollD100 RollConcealmentCheck(RuleConcealmentCheck ruleConcealmentCheck)
        {
            var concealment = CreateConcealmentRoll(NetworkDiceRollType.Hit, ruleConcealmentCheck);
            var deterministicRoll = RollDice(concealment, ruleConcealmentCheck.Roll);

            var d100 = RuleRollD100.FromInt(ruleConcealmentCheck.Initiator, deterministicRoll.Result);
            d100.RollHistory = deterministicRoll.History;
            return d100;
        }

        private RuleRollD20 RollAttackParryData(RuleAttackRoll.ParryData parryData)
        {
            var concentration = CreateParryRoll(NetworkDiceRollType.Hit, parryData);
            var formula = GetDiceFormula(WellKnownDiceFormulaKind.AttackParryData);
            if (formula == null)
            {
                return null;
            }
            var deterministicRoll = RollDice(concentration, formula.Formula, formula.Rerolls);

            var d20 = RuleRollD20.FromInt(parryData.Initiator, deterministicRoll.Result);
            d20.RollHistory = deterministicRoll.History;
            return d20;
        }

        private RuleRollD20 RollCheckConcentration(RuleCheckConcentration ruleCheckConcentration)
        {
            var concentration = CreateConcentrationRoll(NetworkDiceRollType.Hit, ruleCheckConcentration);
            var formula = GetDiceFormula(WellKnownDiceFormulaKind.RuleCheckConcentration);
            if (formula == null)
            {
                return null;
            }
            var deterministicRoll = RollDice(concentration, formula.Formula, formula.Rerolls);

            var d20 = RuleRollD20.FromInt(ruleCheckConcentration.Initiator, deterministicRoll.Result);
            d20.RollHistory = deterministicRoll.History;
            return d20;
        }

        private DeterministicRollOutcome RollDice(NetworkDiceRollBase roll, RuleRollDice ruleRollDice)
        {
            return RollDice(roll, ruleRollDice.DiceFormula, ruleRollDice.m_RerollAmount);
        }

        private DeterministicRollOutcome RollDice(NetworkDiceRollBase roll, DiceFormula diceFormula, int rerollAmount)
        {
            try
            {
                var rollIdentifier = roll.GetIdString();

                var sessionSeed = _multiplayerActorAccessor.Current.SessionSeed;
                var loadedSaveSeed = _multiplayerActorAccessor.Current.LoadedSaveSeed;
                var areaSeed = _multiplayerActorAccessor.Current.AreaSeed;
                var combatSeed = _multiplayerActorAccessor.Current.CombatSeed;
                var combatTurnSeed = _multiplayerActorAccessor.Current.CombatTurnSeed;
                var crusadeCombatSeed = _multiplayerActorAccessor.Current.CrusadeArmyCombatSeed;
                // mid turn damage
                if (combatSeed != null && combatTurnSeed == null)
                {
                    combatTurnSeed = _multiplayerActorAccessor.Current.LastCombatTurnSeed;
                }

                var lifetime = combatTurnSeed == null ? IdentifierLifetime.Area : IdentifierLifetime.CombatTurn;

                var identifier = $"{rollIdentifier}_{sessionSeed}:{loadedSaveSeed}:{areaSeed}:{combatSeed ?? 0}:{combatTurnSeed ?? 0}:{crusadeCombatSeed ?? 0}";
                var (history, result) = RollDice(lifetime, identifier, diceFormula, rerollAmount);
                var outcome = new DeterministicRollOutcome
                {
                    History = history,
                    Identifier = identifier,
                    Result = result,
                    RollId = _hashService.Murmur3(identifier)
                };

                _logger.LogInformation("{RuleName} has been rolled deterministicaly. UnitId={UnitId}, RollId={RollId}, Result={Result}, History={History}, Lifetime={Lifetime}, Identifier={Identifier}",
                    roll.RuleName, roll.InitiatorId, outcome.RollId, outcome.Result, outcome.History, lifetime, outcome.Identifier);

                return outcome;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rolling dice roll deterministically");
                throw;
            }
        }

        private (List<int> history, int result) RollDice(IdentifierLifetime lifetime, string identifier, DiceFormula diceFormula, int rerollAmount)
        {
            var random = _valueGenerator.GetRandom(lifetime, identifier);
            var history = new List<int>();
            var result = 0;
            var rerolls = rerollAmount;
            while (rerolls >= 0)
            {
                result = diceFormula.Roll(random);
                history.Add(result);
                rerolls--;
            }

            return (history, result);
        }

        private RuleRollD100 RetrieveFortificationAttackRoll(RuleAttackRoll ruleAttackRoll)
        {
            var roll = CreateFortificationAttackRoll(NetworkDiceRollType.Hit, ruleAttackRoll);
            var d100 = RetrieveRoll<RuleRollD100>(roll, ruleAttackRoll.Initiator);
            return d100;
        }

        private RuleRollD100 RetrieveAttackOvercomeConcealment(RuleAttackRoll ruleAttackRoll)
        {
            var roll = CreateAttackOvercomeConcealmentRoll(NetworkDiceRollType.Hit, ruleAttackRoll);
            var d100 = RetrieveRoll<RuleRollD100>(roll, ruleAttackRoll.Initiator);
            return d100;
        }

        private RuleRollD20 RetrieveCriticalAttackRoll(RuleAttackRoll ruleAttackRoll)
        {
            var roll = CreateAttackRoll(NetworkDiceRollType.Hit, ruleAttackRoll, isCriticalRoll: true);
            var d20 = RetrieveRoll<RuleRollD20>(roll, ruleAttackRoll.Initiator);
            return d20;
        }

        private RuleRollD20 RetrieveAttackRoll(RuleAttackRoll ruleAttackRoll)
        {
            var roll = CreateAttackRoll(NetworkDiceRollType.Hit, ruleAttackRoll, isCriticalRoll: false);
            var d20 = RetrieveRoll<RuleRollD20>(roll, ruleAttackRoll.Initiator);
            return d20;
        }

        private RuleRollD20 RetrieveInitiativeRoll(RuleInitiativeRoll ruleInitiativeRoll)
        {
            var roll = CreateInitiativeRoll(NetworkDiceRollType.Hit, ruleInitiativeRoll);
            var d20 = RetrieveRoll<RuleRollD20>(roll, ruleInitiativeRoll.Initiator);
            return d20;
        }

        private RuleRollD20 RetrieveSpellResistance(RuleSpellResistanceCheck ruleSpellResistanceCheck)
        {
            var roll = CreateSpellResistanceCheckRoll(NetworkDiceRollType.Hit, ruleSpellResistanceCheck);
            var d20 = RetrieveRoll<RuleRollD20>(roll, ruleSpellResistanceCheck.Initiator);
            return d20;
        }

        private RuleRollD20 RetrieveCheckCastingDefensivelyRoll(RuleCheckCastingDefensively ruleCheckCastingDefensively)
        {
            var roll = CreateCastingDefensivelyRoll(NetworkDiceRollType.Hit, ruleCheckCastingDefensively);
            var d20 = RetrieveRoll<RuleRollD20>(roll, ruleCheckCastingDefensively.Initiator);
            return d20;
        }

        private RuleRollD100 RetrieveConcealmentCheck(RuleConcealmentCheck ruleConcealmentCheck)
        {
            var roll = CreateConcealmentRoll(NetworkDiceRollType.Hit, ruleConcealmentCheck);
            var d100 = RetrieveRoll<RuleRollD100>(roll, ruleConcealmentCheck.Initiator);
            return d100;
        }

        private RuleRollD100 RetrieveCastSpellRoll(RuleCastSpell ruleCastSpell, bool isSpellFailure)
        {
            var roll = CreateCastSpellRoll(NetworkDiceRollType.Hit, ruleCastSpell, isSpellFailure);
            var d100 = RetrieveRoll<RuleRollD100>(roll, ruleCastSpell.Initiator);
            return d100;
        }

        private RuleRollD20 RetrieveSavingThrow(RuleSavingThrow ruleSavingThrow)
        {
            var savingThrow = CreateSavingThrowRoll(NetworkDiceRollType.Hit, ruleSavingThrow);
            var d20 = RetrieveRoll<RuleRollD20>(savingThrow, ruleSavingThrow.Initiator);
            if (d20 == null)
            {
                _logger.LogError("Roll retrieving context={StackTrace}", Environment.StackTrace);
            }

            return d20;
        }

        private RuleRollD20 RetrieveSkillCheck(RuleSkillCheck ruleSkillCheck)
        {
            var roll = CreateSkillCheckRoll(NetworkDiceRollType.Hit, ruleSkillCheck);
            var d20 = RetrieveRoll<RuleRollD20>(roll, ruleSkillCheck.Initiator);
            if (d20 == null)
            {
                _logger.LogError("Roll retrieving context={StackTrace}", Environment.StackTrace);
            }

            return d20;
        }

        private RuleRollD20 RetrieveAttackParryData(RuleAttackRoll.ParryData parryData)
        {
            var roll = CreateParryRoll(NetworkDiceRollType.Hit, parryData);
            var d20 = RetrieveRoll<RuleRollD20>(roll, parryData.Initiator);
            return d20;
        }

        private RuleRollD20 RetrieveDispelMagic(RuleDispelMagic ruleDispelMagic)
        {
            var roll = CreateDispelMagicRoll(NetworkDiceRollType.Hit, ruleDispelMagic);
            var d20 = RetrieveRoll<RuleRollD20>(roll, ruleDispelMagic.Initiator);
            return d20;
        }

        private RuleRollD20 RetrieveEnterStealth(RuleEnterStealth ruleEnterStealth)
        {
            var roll = CreateEnterStealthRoll(NetworkDiceRollType.Hit, ruleEnterStealth);
            var d20 = RetrieveRoll<RuleRollD20>(roll, ruleEnterStealth.Initiator);
            return d20;
        }

        private RuleRollD20 RetrieveCheckConcentration(RuleCheckConcentration ruleCheckConcentration)
        {
            var roll = CreateConcentrationRoll(NetworkDiceRollType.Hit, ruleCheckConcentration);
            var d20 = RetrieveRoll<RuleRollD20>(roll, ruleCheckConcentration.Initiator);
            return d20;
        }

        private int? RetrieveHealDamageRoll(RuleHealDamage ruleHealDamage, bool isTacticalCombat)
        {
            var roll = CreateHealDamageRoll(NetworkDiceRollType.Hit, ruleHealDamage, isTacticalCombat);
            var rollId = GetDiceRollId(roll);
            if (rollId == null)
            {
                _logger.LogWarning("Heal Damage retrieving has been skipped due to unability to generate rollId. InitiatorName={InitiatorName}, InitiatorId={InitiatorId}", ruleHealDamage.Initiator?.CharacterName, ruleHealDamage.Initiator?.UniqueId);
                return null;
            }

            var networkRoll = _multiplayerActorAccessor.Current.RetrieveRoll<NetworkIntRollValue>(rollId.Value, roll.RuleName, ruleHealDamage.Initiator.UniqueId);
            if (networkRoll == null)
            {
                _logger.LogCritical("Failed to acquire heal damage roll from remote player which guarantees desync in the game. RollId={RollId}", rollId.Value);
                _playerNotificationService.AddCombatText(WellKnownKeys.GameNotifications.Rolls.MissingHealingRoll.Key, CombatTextSeverity.Critical, new UnitEntityLog(ruleHealDamage.Initiator.UniqueId));
                return null;
            }

            return networkRoll.Value;
        }

        private enum RuleAttackRollSubType
        {
            None,
            Critical,
            Concealment,
            Fortification
        }
    }
}
