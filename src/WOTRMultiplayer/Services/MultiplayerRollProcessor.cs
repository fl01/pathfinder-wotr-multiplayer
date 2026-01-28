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
using WOTRMultiplayer.Abstractions.Hashing;
using WOTRMultiplayer.Entities.Rolls;
using WOTRMultiplayer.Entities.Rolls.Claiming.Values;
using WOTRMultiplayer.Extensions;

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

        public MultiplayerRollProcessor(
            ILogger<MultiplayerRollProcessor> logger,
            IGameInteractionService gameInteractionService,
            ICombatInteractionService combatInteractionService,
            IPlayerNotificationService playerNotificationService,
            IDiceRollStorage diceRollStorage,
            IHashService hashService,
            IMultiplayerActorAccessor multiplayerActorAccessor)
        {
            _logger = logger;
            _gameInteractionService = gameInteractionService;
            _combatInteractionService = combatInteractionService;
            _playerNotificationService = playerNotificationService;
            _diceRollStorage = diceRollStorage;
            _hashService = hashService;
            _multiplayerActorAccessor = multiplayerActorAccessor;
        }

        public bool OnBeforeRuleCalculateDamageTrigger(RuleCalculateDamage ruleCalculateDamage)
        {
            try
            {
                if (!ShouldRetrieveRoll(ruleCalculateDamage))
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

                    _playerNotificationService.ShowModalMessage(WellKnownKeys.GameNotifications.Rolls.FailedToAcquireRemoteDamageRoll.Key);
                    return true;
                }
                var bundles = ruleCalculateDamage.DamageBundle.ToList();
                if (networkRoll.Value.Count != bundles.Count)
                {
                    _logger.LogCritical("Network damage contains invalid number of damage values. RollId={RollId}, ExpectedCount={ExpectedCount}, ActualCount={ActualCount}", rollId.Value, bundles.Count, networkRoll.Value.Count);
                    _playerNotificationService.ShowModalMessage(WellKnownKeys.GameNotifications.Rolls.InvalidRemoteDamageRoll.Key);
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
                _logger.LogError(ex, "Error before damage rule trigger");
                throw;
            }
        }

        public void OnAfterRuleCalculateDamageTrigger(RuleCalculateDamage ruleCalculateDamage)
        {
            try
            {
                if (!ShouldStoreRoll(ruleCalculateDamage))
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

        public bool OnBeforeRollRuleHealDamage(RuleHealDamage ruleHealDamage, int unitsCount, bool isTacticalCombat)
        {
            try
            {
                if (!ShouldRetrieveRoll(ruleHealDamage))
                {
                    return true;
                }

                var roll = CreateHealDamageRoll(NetworkDiceRollType.Hit, ruleHealDamage, unitsCount, isTacticalCombat);
                var rollId = GetDiceRollId(roll);
                if (rollId == null)
                {
                    _logger.LogWarning("Heal Damage retrieving has been skipped due to unability to generate rollId. InitiatorName={InitiatorName}, InitiatorId={InitiatorId}", ruleHealDamage.Initiator?.CharacterName, ruleHealDamage.Initiator?.UniqueId);
                    return true;
                }

                var networkRoll = _multiplayerActorAccessor.Current.RetrieveRoll<NetworkNamedIntRollValue>(rollId.Value, roll.RuleName, ruleHealDamage.Initiator.UniqueId);
                if (networkRoll == null)
                {
                    _logger.LogCritical("Failed to acquire heal damage roll from remote player which guarantees desync in the game. RollId={RollId}", rollId.Value);
                    _playerNotificationService.ShowModalMessage(WellKnownKeys.GameNotifications.Rolls.FailedToAcquireRemoteHealRoll.Key);
                    return true;
                }

                networkRoll.Value.TryGetValue(nameof(ruleHealDamage.Bonus), out var bonusValue);
                ruleHealDamage.GetType()
                  .GetProperty(nameof(ruleHealDamage.Bonus))
                  .SetPropertyValue(ruleHealDamage, bonusValue);

                networkRoll.Value.TryGetValue(nameof(ruleHealDamage.RollResult), out var rollResult);
                ruleHealDamage.RollResult = rollResult;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error before heal rule trigger");
                throw;
            }
        }

        public void OnAfterRollRuleHealDamage(RuleHealDamage ruleHealDamage, int unitsCount, int result, bool isTacticalCombat)
        {
            try
            {
                if (!ShouldStoreRoll(ruleHealDamage))
                {
                    return;
                }

                var roll = CreateHealDamageRoll(NetworkDiceRollType.Hit, ruleHealDamage, unitsCount, isTacticalCombat);
                var rollId = GetDiceRollId(roll);
                if (rollId == null)
                {
                    _logger.LogWarning("Heal Damage saving has been skipped due to unability to generate rollId. InitiatorName={InitiatorName}, InitiatorId={InitiatorId}", ruleHealDamage.Initiator?.CharacterName, ruleHealDamage.Initiator?.UniqueId);
                    return;
                }

                var rollValue = new NetworkNamedIntRollValue
                {
                    Value = new Dictionary<string, int>
                    {
                        { nameof(ruleHealDamage.Bonus), ruleHealDamage.Bonus },
                        { nameof(ruleHealDamage.RollResult), result },
                    }
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
                if (!ShouldRetrieveRoll(ruleAttackRoll))
                {
                    return true;
                }

                var roll = CreateAttackOvercomeConcealmentRoll(NetworkDiceRollType.Hit, ruleAttackRoll);
                var d100 = RetrieveRoll<RuleRollD100>(roll, ruleAttackRoll.Initiator);
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
                if (!ShouldStoreRoll(ruleAttackRoll) || ruleAttackRoll.MissChanceRoll == null)
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
                if (!ShouldRetrieveRoll(ruleAttackRoll))
                {
                    return true;
                }

                var roll = CreateFortificationAttackRoll(NetworkDiceRollType.Hit, ruleAttackRoll);
                var d100 = RetrieveRoll<RuleRollD100>(roll, ruleAttackRoll.Initiator);
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
                if (!ShouldRetrieveRoll(ruleAttackRoll))
                {
                    return true;
                }

                var roll = CreateAttackRoll(NetworkDiceRollType.Hit, ruleAttackRoll, ruleAttackRoll.IsCriticalRoll);
                var d20 = RetrieveRoll<RuleRollD20>(roll, ruleAttackRoll.Initiator);
                if (d20 == null)
                {
                    return true;
                }

                if (ruleAttackRoll.IsCriticalRoll)
                {
                    ruleAttackRoll.CriticalConfirmationD20 = d20;
                }
                else
                {
                    ruleAttackRoll.D20 = d20;
                }

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

                var roll = CreateAttackRoll(NetworkDiceRollType.Hit, ruleAttackRoll, false);
                SaveIntRollValue(roll, ruleAttackRoll.D20);
                if (ruleAttackRoll.IsCriticalRoll)
                {
                    var criticalRoll = CreateAttackRoll(NetworkDiceRollType.Hit, ruleAttackRoll, true);
                    SaveIntRollValue(criticalRoll, ruleAttackRoll.CriticalConfirmationD20);
                }

                if (ruleAttackRoll.FortificationRoll != null)
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
                if (!ShouldStoreRoll(ruleSavingThrow))
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

        public void OnBeforeRuleSavingThrowRoll(RuleSavingThrow ruleSavingThrow)
        {
            try
            {
                if (!ShouldRetrieveRoll(ruleSavingThrow))
                {
                    return;
                }

                var savingThrow = CreateSavingThrowRoll(NetworkDiceRollType.Hit, ruleSavingThrow);
                ruleSavingThrow.D20 = RetrieveRoll<RuleRollD20>(savingThrow, ruleSavingThrow.Initiator);
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
                if (!ShouldRetrieveRoll(ruleSpellResistanceCheck))
                {
                    return true;
                }

                var roll = CreateSpellResistanceCheckRoll(NetworkDiceRollType.Hit, ruleSpellResistanceCheck);
                var d20 = RetrieveRoll<RuleRollD20>(roll, ruleSpellResistanceCheck.Initiator);
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
                if (!ShouldStoreRoll(ruleSpellResistanceCheck) || ruleSpellResistanceCheck.Roll == null)
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
                if (!ShouldRetrieveRoll(ruleCheckConcentration))
                {
                    return true;
                }

                var roll = CreateConcentrationRoll(NetworkDiceRollType.Hit, ruleCheckConcentration);
                var d20 = RetrieveRoll<RuleRollD20>(roll, ruleCheckConcentration.Initiator);
                if (d20 == null)
                {
                    return true;
                }

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
                if (!ShouldStoreRoll(ruleCheckConcentration))
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
                if (!ShouldRetrieveRoll(ruleSkillCheck))
                {
                    return true;
                }

                var roll = CreateSkillCheckRoll(NetworkDiceRollType.Hit, ruleSkillCheck);
                var d20 = RetrieveRoll<RuleRollD20>(roll, ruleSkillCheck.Initiator);
                if (d20 == null)
                {
                    _logger.LogInformation("Roll retrieving context={StackTrace}", Environment.StackTrace);
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
                if (!ShouldStoreRoll(ruleSkillCheck))
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
                if (!ShouldRetrieveRoll(ruleInitiativeRoll))
                {
                    return true;
                }

                var roll = CreateInitiativeRoll(NetworkDiceRollType.Hit, ruleInitiativeRoll);
                var d20 = RetrieveRoll<RuleRollD20>(roll, ruleInitiativeRoll.Initiator);
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
                if (!ShouldStoreRoll(ruleInitiativeRoll))
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
                if (!ShouldRetrieveRoll(ruleConcealmentCheck))
                {
                    return true;
                }

                var roll = CreateConcealmentRoll(NetworkDiceRollType.Hit, ruleConcealmentCheck);
                var d100 = RetrieveRoll<RuleRollD100>(roll, ruleConcealmentCheck.Initiator);
                if (d100 == null)
                {
                    return true;
                }

                // TODO: cache reflection
                ruleConcealmentCheck.GetType()
                    .GetProperty(nameof(RuleConcealmentCheck.Roll))
                    .SetPropertyValue(ruleConcealmentCheck, d100);

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
                if (!ShouldStoreRoll(ruleConcealmentCheck) || ruleConcealmentCheck.ConcealmentValue <= 0)
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
                if (!ShouldRetrieveRoll(parryData))
                {
                    return true;
                }

                var roll = CreateParryRoll(NetworkDiceRollType.Hit, parryData);
                var d20 = RetrieveRoll<RuleRollD20>(roll, parryData.Initiator);
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
                if (!ShouldStoreRoll(parryData))
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
                if (!ShouldRetrieveRoll(ruleDispelMagic))
                {
                    return true;
                }

                var roll = CreateDispelMagicRoll(NetworkDiceRollType.Hit, ruleDispelMagic);
                var d20 = RetrieveRoll<RuleRollD20>(roll, ruleDispelMagic.Initiator);
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
                if (!ShouldStoreRoll(ruleDispelMagic))
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
                if (!ShouldRetrieveRoll(ruleEnterStealth))
                {
                    return true;
                }

                var roll = CreateEnterStealthRoll(NetworkDiceRollType.Hit, ruleEnterStealth);
                var d20 = RetrieveRoll<RuleRollD20>(roll, ruleEnterStealth.Initiator);
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
                if (!ShouldStoreRoll(ruleEnterStealth))
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
                if (!ShouldRetrieveRoll(ruleDrainEnergy))
                {
                    return true;
                }

                var roll = CreateDrainEnergyRoll(NetworkDiceRollType.Damage, ruleDrainEnergy, ruleRollDice);
                var d100 = RetrieveRoll<RuleRollD100>(roll, ruleDrainEnergy.Initiator);
                if (d100 == null)
                {
                    return true;
                }

                ruleRollDice.m_Result = d100.m_Result;
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
                if (!ShouldStoreRoll(ruleDrainEnergy))
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

                return d100.m_Result;
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

                var networkDamageRoll = CreateDealStatDamageRoll(NetworkDiceRollType.Damage, ruleDealStatDamage, criticalModifier);
                SaveIntRollValue(networkDamageRoll, damageRoll);
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
                if (!ShouldRetrieveRoll(ruleCastSpell))
                {
                    return true;
                }

                var roll = CreateCastSpellRoll(NetworkDiceRollType.Hit, ruleCastSpell, isSpellFailure);
                var d100 = RetrieveRoll<RuleRollD100>(roll, ruleCastSpell.Initiator);
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
                if (!ShouldStoreRoll(ruleCastSpell))
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
                if (!ShouldRetrieveRoll(ruleCheckCastingDefensively))
                {
                    return true;
                }

                var roll = CreateCastingDefensivelyRoll(NetworkDiceRollType.Hit, ruleCheckCastingDefensively);
                var d20 = RetrieveRoll<RuleRollD20>(roll, ruleCheckCastingDefensively.Initiator);
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
                if (!ShouldStoreRoll(ruleCheckCastingDefensively) || ruleCheckCastingDefensively.AutoFail)
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
                return rule is RuleSkillCheck or RuleSavingThrow;
            }

            if (gameMode == GameModeType.Cutscene || gameMode == GameModeType.CutsceneGlobalMap)
            {
                return false;
            }

            if (!_combatInteractionService.IsInCrusadeTacticalCombat())
            {
                switch (rule)
                {
                    case RuleCalculateDamage:
                    case RuleHealDamage:
                    case RuleDealDamage:
                        var targetEvent = (RulebookTargetEvent)rule;
                        var initiator = targetEvent.Initiator?.UniqueId;
                        var target = targetEvent.Target?.UniqueId;
                        var affectsControlledCharacters = _combatInteractionService.IsInCombat() || _multiplayerActorAccessor.Current.IsControlledByPlayers(initiator) || _multiplayerActorAccessor.Current.IsControlledByPlayers(target);
                        return affectsControlledCharacters;
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

        private int? GetDamageRollId(RuleCalculateDamage ruleCalculateDamage)
        {
            NetworkDiceRollBase roll = ruleCalculateDamage.Reason.Rule switch
            {
                RuleAttackWithWeapon ruleAttackWithWeapon => CreateAttackWithWeaponRoll(NetworkDiceRollType.Damage, ruleAttackWithWeapon),
                null => CreateAbilityUse(NetworkDiceRollType.Damage, ruleCalculateDamage),
                _ => null,
            };

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
                    _playerNotificationService.ShowModalMessage(WellKnownKeys.GameNotifications.Rolls.FailedToAcquireRemoteRoll.Key, networkDiceRoll.RuleName);
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
            var roll = new SkillCheckRoll(ruleSkillCheck.Initiator.UniqueId, ruleSkillCheck.GetType().Name, diceRollType, ruleSkillCheck.TotalBonus)
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

        private HealDamageRoll CreateHealDamageRoll(NetworkDiceRollType diceRollType, RuleHealDamage ruleHealDamage, int unitsCount, bool isTacticalCombat)
        {
            var roll = new HealDamageRoll(ruleHealDamage.Initiator?.UniqueId, ruleHealDamage.GetType().Name, diceRollType, 0)
            {
                AbilityName = ruleHealDamage.Reason.Ability?.StickyTouch?.NameForAcronym ?? ruleHealDamage.Reason.Ability?.NameForAcronym,
                AbilitySchoolId = ruleHealDamage.Reason.Ability?.Spellbook?.Blueprint.name,
                TargetId = ruleHealDamage.Target?.UniqueId,
                UnitsCount = unitsCount,
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

        private SavingThrowRoll CreateSavingThrowRoll(NetworkDiceRollType diceRollType, RuleSavingThrow ruleSavingThrow)
        {
            // totalbonus is not calculated before roll so it can't be used to generate unique id
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
    }
}
