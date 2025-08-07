using System;
using System.Linq;
using System.Reflection;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Persistence;
using Kingmaker.GameModes;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.RuleSystem.Rules.Damage;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.Hashing;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Windows;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.GameInteraction.Contexts;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Abilities;
using WOTRMultiplayer.MP.Entities.Equipment;
using WOTRMultiplayer.MP.Entities.Loot;
using WOTRMultiplayer.MP.Entities.MapObjects;
using WOTRMultiplayer.MP.Entities.Rolls;
using WOTRMultiplayer.MP.Entities.Rolls.Claiming.Values;
using WOTRMultiplayer.UI.Menu;

namespace WOTRMultiplayer.MP
{
    public class Multiplayer : IMultiplayer
    {
        private IMultiplayerWindow _multiplayerWindow;
        private ILobbyWindow _lobbyWindow;

        private readonly ILobbyWindowController _lobbyWindowController;
        private readonly IHashService _hashService;
        private readonly IDiceRollStorage _diceRollStorage;
        private readonly IGameInteractionService _gameInteractionService;
        private readonly IMultiplayerClient _multiplayerClient;
        private readonly IMultiplayerHost _multiplayerHost;
        private readonly ILogger _logger;

        public IUIFactory Factory { get; private set; }
        public IUniqueIdGenerator IdGenerator { get; private set; }

        public bool IsActive => _multiplayerClient.IsActive || _multiplayerHost.IsActive;

        public bool IsInCombat => IsActive && (_multiplayerClient.IsInCombat || _multiplayerHost.IsInCombat);

        public NetworkExecutionContext ExecutionContext => _gameInteractionService.ExecutionContext;

        public Multiplayer(
            ILogger<Multiplayer> logger,
            IUIFactory uiFactory,
            ILobbyWindowController lobbyWindowController,
            IMultiplayerHost multiplayerHost,
            IMultiplayerClient multiplayerClient,
            IHashService hashService,
            IDiceRollStorage diceRollStorage,
            IGameInteractionService gameInteractionService,
            IUniqueIdGenerator uniqueIdGenerator)
        {
            _logger = logger;
            Factory = uiFactory;
            _multiplayerHost = multiplayerHost;
            _multiplayerClient = multiplayerClient;
            _lobbyWindowController = lobbyWindowController;
            _hashService = hashService;
            _diceRollStorage = diceRollStorage;
            _gameInteractionService = gameInteractionService;
            IdGenerator = uniqueIdGenerator;
        }

        public bool InitializeMultiplayer(InitializeMultiplayerContext context)
        {
            if (_multiplayerHost.IsActive)
            {
                _logger.LogWarning("Multiplayer host has not been properly disposed. Verify exit game/main menu handles");
                _multiplayerHost.Dispose();
            }

            if (_multiplayerClient.IsActive)
            {
                _logger.LogWarning("Multiplayer client has not been properly disposed. Verify exit game/main menu handlers");
                _multiplayerClient.Dispose();
            }

            _multiplayerWindow = Factory.InitializeMultiplayerWindow(context, ShowMultiplayerWindow);

            return true;
        }

        public void TerminateMultiplayer()
        {
            _logger.LogInformation("Disposing both multiplayer host/client");
            _multiplayerHost.Dispose();
            _multiplayerClient.Dispose();
            _lobbyWindowController.ResetOwnerContent(LobbyWindowOwner.EscMenu);
            _lobbyWindowController.OnCharacterOwnerChanged = null;
            _logger.LogInformation("Disposing Esc menu window game objects");
            Factory.DestroyLobbyWindow(_lobbyWindow);
            _logger.LogInformation("Disposing stored rolls");
        }

        public void InitializeEscMenuLobbyWindow(InitializeEscMenuLobbyWindowContext context)
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return;
            }

            _logger.LogInformation("Creating Esc menu multiplayer lobby window");
            _lobbyWindow = Factory.InitializeEscMenuLobbyWindow(context, _multiplayerHost.IsActive, ShowEscMenuMultiplayerLobby);

            _lobbyWindow.GetGameConnectivity = multiplayerActor.GetGameConnectivity;
            _lobbyWindow.GetPlayers = multiplayerActor.GetPlayers;
            _lobbyWindow.GetCharacters = multiplayerActor.GetCharacters;

            _lobbyWindow.AssignLobbyController(_lobbyWindowController);

            _lobbyWindowController.OnCharacterOwnerChanged = OnLobbyCharacterOwnerChanged;
        }

        public void MoveNonCombatCharacter(string unitId, NetworkVector3 destination, float delay, float orientation)
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return;
            }

            multiplayerActor.MoveNonCombatCharacter(unitId, destination, delay, orientation);
        }


        public string GetMultiplayerOwnerName(string unitId)
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return null;
            }

            return multiplayerActor.GetMultiplayerOwnerName(unitId);

        }
        public bool IsControlledByLocalPlayer(string unitId)
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return false;
            }

            return multiplayerActor.IsControlledByLocalPlayer(unitId);
        }

        public bool StartGameMode(GameModeType type)
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return true;
            }

            var allowedToRun = type != GameModeType.EscMode && type != GameModeType.FullScreenUi;
            _logger.LogInformation("Trying to start GameModeType. Mode={mode}, AllowedToRun={allowedToRun}", type.Name, allowedToRun);

            if (type == GameModeType.Pause)
            {
                multiplayerActor.Pause();
            }

            return allowedToRun;
        }

        public bool StopGameMode(GameModeType type)
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return true;
            }

            _logger.LogInformation("Trying to stop GameModeType. Mode={mode}", type.Name);

            if (type == GameModeType.Pause)
            {
                multiplayerActor.Unpause();
            }

            return true;
        }

        public bool CanLeaveArea()
        {
            return !_multiplayerClient.IsActive;
        }

        public bool OnBeforeRuleCalculateDamageTrigger(RuleCalculateDamage ruleCalculateDamage)
        {
            try
            {
                var multiplayerActor = GetMultiplayerActor();
                if (!ShouldRetrieveRoll(multiplayerActor, ruleCalculateDamage))
                {
                    return true;
                }

                var rollId = GetDamageRollId(ruleCalculateDamage);
                if (rollId == null)
                {
                    _logger.LogWarning("Damage Roll retrieving has been skipped due to unability to generate rollId. InitiatorName={initiatorName}, InitiatorId={initiatorId}", ruleCalculateDamage.Initiator?.CharacterName, ruleCalculateDamage.Initiator?.UniqueId);
                    return true;
                }

                var networkRoll = multiplayerActor.RetrieveRoll<NetworkDamageListRollValue>(rollId.Value, ruleCalculateDamage.Initiator.UniqueId);

                if (networkRoll == null)
                {
                    _logger.LogCritical("Failed to acquire damage roll from remote player which guarantees desync in the game. RollId={rollId}", rollId.Value);
                    _gameInteractionService.ShowModalMessage($"Failed to acquire damage roll from remote player which guarantees desync in the game");
                    return true;
                }
                var bundles = ruleCalculateDamage.DamageBundle.ToList();
                if (networkRoll.Value.Count != bundles.Count)
                {
                    _logger.LogCritical("Network damage contains invalid number of damage values. RollId={rollId}, ExpectedCount={expectedCount}, ActualCount={actualCount}", rollId.Value, bundles.Count, networkRoll.Value.Count);
                    _gameInteractionService.ShowModalMessage($"Network damage contains invalid number of damage values which guarantees desync in the game");
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

                _logger.LogInformation("Damage roll result has been acquired from another player. RollId={rollId}, DamageValuesCount={damageValuesCount}", rollId.Value, ruleCalculateDamage.CalculatedDamage.Count);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {methodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public void OnAfterRuleCalculateDamageTrigger(RuleCalculateDamage ruleCalculateDamage)
        {
            try
            {
                var multiplayerActor = GetMultiplayerActor();
                if (!ShouldStoreRoll(multiplayerActor, ruleCalculateDamage))
                {
                    return;
                }

                var rollId = GetDamageRollId(ruleCalculateDamage);
                if (rollId == null)
                {
                    _logger.LogWarning("Damage Roll saving has been skipped due to unability to generate rollId. InitiatorName={initiatorName}, InitiatorId={initiatorId}", ruleCalculateDamage.Initiator?.CharacterName, ruleCalculateDamage.Initiator?.UniqueId);
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

                SaveRollValue(multiplayerActor, rollId.Value, rollValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {methodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public int OnAfterRollRuleHealDamage(RuleHealDamage ruleHealDamage, int unitsCount, int result)
        {
            try
            {
                var multiplayerActor = GetMultiplayerActor();
                if (multiplayerActor == null)
                {
                    return result;
                }

                var roll = CreateHealDamageRoll(NetworkDiceRollType.Heal, ruleHealDamage, unitsCount);
                var rollId = GetDiceRollId(roll);
                if (rollId == null)
                {
                    _logger.LogInformation("Unable to get rollId for healing, SourceFactId={factId}, CasterId={casterId}, TargetId={targetId}, EmpowerBonus={emopower}, Result={result}",
                        ruleHealDamage.SourceFact?.UniqueId, ruleHealDamage.Initiator.UniqueId, ruleHealDamage.Target.UniqueId, ruleHealDamage.EmpowerModifier, result);
                    return result;
                }

                if (multiplayerActor.IsDiceRollOwner(false))
                {
                    var value = new NetworkIntRollValue { Value = result };
                    SaveRollValue(multiplayerActor, rollId.Value, value);
                    return result;
                }

                var d20 = RetrieveRoll<RuleRollD20>(multiplayerActor, roll, ruleHealDamage.Initiator);
                return d20.Result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {methodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public bool OnBeforeRuleAttackOvercomeConcealmentRoll(RuleAttackRoll ruleAttackRoll)
        {
            try
            {
                var multiplayerActor = GetMultiplayerActor();
                if (!ShouldRetrieveRoll(multiplayerActor, ruleAttackRoll))
                {
                    return true;
                }

                var roll = CreateAttackOvercomeConcealmentRoll(NetworkDiceRollType.Hit, ruleAttackRoll);
                var d100 = RetrieveRoll<RuleRollD100>(multiplayerActor, roll, ruleAttackRoll.Initiator);
                if (d100 == null)
                {
                    return true;
                }

                ruleAttackRoll.MissChanceRoll = d100;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {methodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public void OnAfterRuleAttackOvercomeConcealmentRoll(RuleAttackRoll ruleAttackRoll)
        {
            try
            {
                var multiplayerActor = GetMultiplayerActor();
                if (!ShouldStoreRoll(multiplayerActor, ruleAttackRoll) || ruleAttackRoll.MissChanceRoll == null)
                {
                    return;
                }

                var roll = CreateAttackOvercomeConcealmentRoll(NetworkDiceRollType.Hit, ruleAttackRoll);
                SaveIntRollValue(multiplayerActor, roll, ruleAttackRoll.MissChanceRoll);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {methodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public bool OnBeforeRuleAttackFortificationRoll(RuleAttackRoll ruleAttackRoll)
        {
            try
            {
                var multiplayerActor = GetMultiplayerActor();
                if (!ShouldRetrieveRoll(multiplayerActor, ruleAttackRoll))
                {
                    return true;
                }

                var roll = CreateFortificationAttackRoll(NetworkDiceRollType.Hit, ruleAttackRoll);
                var d100 = RetrieveRoll<RuleRollD100>(multiplayerActor, roll, ruleAttackRoll.Initiator);
                if (d100 == null)
                {
                    return true;
                }

                ruleAttackRoll.FortificationRoll = d100;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {methodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public bool OnBeforeRuleAttackRoll(RuleAttackRoll ruleAttackRoll, bool isCriticalRoll)
        {
            try
            {
                var multiplayerActor = GetMultiplayerActor();
                if (!ShouldRetrieveRoll(multiplayerActor, ruleAttackRoll))
                {
                    return true;
                }

                var roll = CreateAttackRoll(NetworkDiceRollType.Hit, ruleAttackRoll, isCriticalRoll);
                var d20 = RetrieveRoll<RuleRollD20>(multiplayerActor, roll, ruleAttackRoll.Initiator);
                if (d20 == null)
                {
                    return true;
                }

                if (isCriticalRoll)
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
                _logger.LogError(ex, "Unable to handle {methodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public void OnAfterRuleAttackRollTrigger(RuleAttackRoll ruleAttackRoll)
        {
            try
            {
                var multiplayerActor = GetMultiplayerActor();
                if (!ShouldStoreRoll(multiplayerActor, ruleAttackRoll) || ruleAttackRoll.D20 == null)
                {
                    return;
                }

                var roll = CreateAttackRoll(NetworkDiceRollType.Hit, ruleAttackRoll, false);
                SaveIntRollValue(multiplayerActor, roll, ruleAttackRoll.D20);
                if (ruleAttackRoll.IsCriticalRoll)
                {
                    var criticalRoll = CreateAttackRoll(NetworkDiceRollType.Hit, ruleAttackRoll, true);
                    SaveIntRollValue(multiplayerActor, criticalRoll, ruleAttackRoll.CriticalConfirmationD20);
                }

                if (ruleAttackRoll.FortificationRoll != null)
                {
                    var fortificationRoll = CreateFortificationAttackRoll(NetworkDiceRollType.Hit, ruleAttackRoll);
                    SaveIntRollValue(multiplayerActor, fortificationRoll, ruleAttackRoll.FortificationRoll);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {methodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public void OnAfterRuleSavingThrowTrigger(RuleSavingThrow ruleSavingThrow)
        {
            try
            {
                var multiplayerActor = GetMultiplayerActor();
                if (!ShouldStoreRoll(multiplayerActor, ruleSavingThrow))
                {
                    return;
                }

                var savingThrow = CreateSavingThrowRoll(NetworkDiceRollType.Hit, ruleSavingThrow);
                SaveIntRollValue(multiplayerActor, savingThrow, ruleSavingThrow.D20);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {methodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public void OnBeforeRuleSavingThrowRoll(RuleSavingThrow ruleSavingThrow)
        {
            try
            {
                var multiplayerActor = GetMultiplayerActor();
                if (!ShouldRetrieveRoll(multiplayerActor, ruleSavingThrow))
                {
                    return;
                }

                var savingThrow = CreateSavingThrowRoll(NetworkDiceRollType.Hit, ruleSavingThrow);
                ruleSavingThrow.D20 = RetrieveRoll<RuleRollD20>(multiplayerActor, savingThrow, ruleSavingThrow.Initiator);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {methodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public bool OnBeforeRuleSpellResistanceCheckRoll(RuleSpellResistanceCheck ruleSpellResistanceCheck)
        {
            try
            {
                var multiplayerActor = GetMultiplayerActor();
                if (!ShouldRetrieveRoll(multiplayerActor, ruleSpellResistanceCheck))
                {
                    return true;
                }

                var roll = CreateSpellResistanceCheckRoll(NetworkDiceRollType.Hit, ruleSpellResistanceCheck);
                var d20 = RetrieveRoll<RuleRollD20>(multiplayerActor, roll, ruleSpellResistanceCheck.Initiator);
                if (d20 == null)
                {
                    return true;
                }

                ruleSpellResistanceCheck.Roll = d20;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {methodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public void OnAfterRuleSpellResistanceCheckTrigger(RuleSpellResistanceCheck ruleSpellResistanceCheck)
        {
            try
            {
                var multiplayerActor = GetMultiplayerActor();
                if (!ShouldStoreRoll(multiplayerActor, ruleSpellResistanceCheck))
                {
                    return;
                }

                var roll = CreateSpellResistanceCheckRoll(NetworkDiceRollType.Hit, ruleSpellResistanceCheck);
                SaveIntRollValue(multiplayerActor, roll, ruleSpellResistanceCheck.Roll);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {methodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public bool OnBeforeRuleCheckConcentrationRoll(RuleCheckConcentration ruleCheckConcentration)
        {
            try
            {
                var multiplayerActor = GetMultiplayerActor();
                if (!ShouldRetrieveRoll(multiplayerActor, ruleCheckConcentration))
                {
                    return true;
                }

                var roll = CreateConcentrationRoll(NetworkDiceRollType.Hit, ruleCheckConcentration);
                var d20 = RetrieveRoll<RuleRollD20>(multiplayerActor, roll, ruleCheckConcentration.Initiator);
                if (d20 == null)
                {
                    return true;
                }

                ruleCheckConcentration.ResultRollRaw = d20;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {methodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public void OnAfterRuleCheckConcentrationTrigger(RuleCheckConcentration ruleCheckConcentration)
        {
            try
            {
                var multiplayerActor = GetMultiplayerActor();
                if (!ShouldStoreRoll(multiplayerActor, ruleCheckConcentration))
                {
                    return;
                }

                var roll = CreateConcentrationRoll(NetworkDiceRollType.Hit, ruleCheckConcentration);
                SaveIntRollValue(multiplayerActor, roll, ruleCheckConcentration.ResultRollRaw);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {methodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public bool OnBeforeRuleSkillCheckRoll(RuleSkillCheck ruleSkillCheck)
        {
            try
            {
                var multiplayerActor = GetMultiplayerActor();
                if (!ShouldRetrieveRoll(multiplayerActor, ruleSkillCheck))
                {
                    return true;
                }

                var roll = CreateSkillCheckRoll(NetworkDiceRollType.Hit, ruleSkillCheck);
                var d20 = RetrieveRoll<RuleRollD20>(multiplayerActor, roll, ruleSkillCheck.Initiator);
                if (d20 == null)
                {
                    return true;
                }

                ruleSkillCheck.D20 = d20;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {methodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public void OnAfterRuleSkillCheckTrigger(RuleSkillCheck ruleSkillCheck)
        {
            try
            {
                var multiplayerActor = GetMultiplayerActor();
                if (!ShouldStoreRoll(multiplayerActor, ruleSkillCheck))
                {
                    return;
                }

                var roll = CreateSkillCheckRoll(NetworkDiceRollType.Hit, ruleSkillCheck);
                SaveIntRollValue(multiplayerActor, roll, ruleSkillCheck.D20);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {methodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }


        public bool OnBeforeRuleInitiativeRoll(RuleInitiativeRoll ruleInitiativeRoll)
        {
            try
            {
                var multiplayerActor = GetMultiplayerActor();
                if (!ShouldRetrieveRoll(multiplayerActor, ruleInitiativeRoll))
                {
                    return true;
                }

                var roll = CreateInitiativeRoll(NetworkDiceRollType.Hit, ruleInitiativeRoll);
                var d20 = RetrieveRoll<RuleRollD20>(multiplayerActor, roll, ruleInitiativeRoll.Initiator);
                if (d20 == null)
                {
                    return true;
                }


                ruleInitiativeRoll.D20 = d20;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {methodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public void OnAfterRuleInitiativeRollTrigger(RuleInitiativeRoll ruleInitiativeRoll)
        {
            try
            {
                var multiplayerActor = GetMultiplayerActor();
                if (!ShouldStoreRoll(multiplayerActor, ruleInitiativeRoll))
                {
                    return;
                }

                var roll = CreateInitiativeRoll(NetworkDiceRollType.Hit, ruleInitiativeRoll);
                SaveIntRollValue(multiplayerActor, roll, ruleInitiativeRoll.D20);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {methodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public bool OnBeforeRuleConcealmentCheckTrigger(RuleConcealmentCheck ruleConcealmentCheck)
        {
            try
            {
                var multiplayerActor = GetMultiplayerActor();
                if (!ShouldRetrieveRoll(multiplayerActor, ruleConcealmentCheck))
                {
                    return true;
                }

                var roll = CreateConcealmentRoll(NetworkDiceRollType.Hit, ruleConcealmentCheck);
                var d100 = RetrieveRoll<RuleRollD100>(multiplayerActor, roll, ruleConcealmentCheck.Initiator);
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
                _logger.LogError(ex, "Unable to handle {methodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public void OnAfterRuleConcealmentCheckTrigger(RuleConcealmentCheck ruleConcealmentCheck)
        {
            try
            {
                var multiplayerActor = GetMultiplayerActor();
                if (!ShouldStoreRoll(multiplayerActor, ruleConcealmentCheck))
                {
                    return;
                }

                var roll = CreateConcealmentRoll(NetworkDiceRollType.Hit, ruleConcealmentCheck);
                SaveIntRollValue(multiplayerActor, roll, ruleConcealmentCheck.Roll);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {methodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public void OnAfterCueShow(string dialogName, string cueName, bool hasSystemAnswer)
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return;
            }

            multiplayerActor.OnAfterCueShow(dialogName, cueName, hasSystemAnswer);
        }

        public bool OnBeforeSelectDialogAnswer(string dialogName, string cueName, string answerName, bool isExitAnswer, string manualUnitSelectionId)
        {
            // host - check if everyone witnessed current cue
            // client - skip execution if triggered by user himself, send notification to host => mark answer on host side
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return true;
            }

            var shouldContinueExecution = multiplayerActor.OnBeforeSelectDialogAnswer(dialogName, cueName, answerName, isExitAnswer, manualUnitSelectionId);
            return shouldContinueExecution;
        }

        public void OnAfterPlayDialogCue()
        {
            if (_multiplayerClient.IsActive)
            {
                return;
            }

            _multiplayerHost.SendSelectedAnswer();
        }

        public bool StartDialog(string dialogName, string targetUnitId, string initiatorUnitId, string mapObjectId, string speakerKey)
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return true;
            }

            _logger.LogInformation("Start dialog. DialogueName={dialogName},  TargetUnitId={targetUnitId}, InitiatorUnitId={initiatorUnitId}, MapObjectId={mapObjectId}, SpeakerKey={speakerKey}",
                dialogName, targetUnitId, initiatorUnitId, mapObjectId, speakerKey);

            return multiplayerActor.StartDialog(dialogName, targetUnitId, initiatorUnitId, mapObjectId, speakerKey);
        }

        public bool CanTickUnitCombatPrepareController()
        {
            try
            {
                var multiplayerActor = GetMultiplayerActor();
                if (multiplayerActor == null)
                {
                    return true;
                }

                return multiplayerActor.CanInitializeCombat();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to initiatize combat");
                throw;
            }
        }

        public bool CanTickCombatController()
        {
            try
            {
                var multiplayerActor = GetMultiplayerActor();
                if (multiplayerActor == null)
                {
                    return true;
                }

                return multiplayerActor.CanContinueCombat();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to continue combat");
                throw;
            }
        }

        public bool OnBeforeStartTurn(string unitId, bool actingInSurpriseRound)
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return true;
            }

            return multiplayerActor.OnBeforeStartTurn(unitId, actingInSurpriseRound);
        }

        public bool OnBeforeEndTurn(string unitId)
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return true;
            }

            return multiplayerActor.OnBeforeEndTurn(unitId);
        }

        public void ForceLoadGame(SaveInfo saveInfo)
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return;
            }

            // extra validation is not required since everything is already validated by the game
            var savePath = saveInfo.FolderName;
            _logger.LogInformation("Force load game. Save={saveLocation}, GameId={gameId}", savePath, saveInfo.GameId);
            multiplayerActor.ForceLoadGame(savePath, saveInfo.GameId);
        }

        public bool IsControlledByPlayers(string unitId)
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return true;
            }

            var result = multiplayerActor.IsControlledByPlayers(unitId);
            return result;
        }

        public void OnClickUnit(NetworkClick click)
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return;
            }

            multiplayerActor.OnClickUnit(click);
        }

        public void OnClickGround(NetworkClick click)
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return;
            }

            multiplayerActor.OnClickGround(click);
        }

        public void OnClickMapObject(NetworkClick click)
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return;
            }

            multiplayerActor.OnClickMapObject(click);
        }

        public void OnAbilityUse(NetworkAbility ability)
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return;
            }

            multiplayerActor.OnAbilityUse(ability);
        }

        public void OnToggleActivatableAbility(NetworkActivatableAbility activatableAbilityUse)
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return;
            }

            multiplayerActor.OnToggleActivatableAbility(activatableAbilityUse);
        }

        public NetworkActionsState GetActionsState()
        {
            if (GetMultiplayerActor() == null)
            {
                return null;
            }

            var actionsState = _gameInteractionService.GetActionsState();
            return actionsState;
        }

        public bool CanLootUnit(string initiatorUnitId)
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return true;
            }

            var isControlledByLocalPlayer = multiplayerActor.IsControlledByLocalPlayer(initiatorUnitId);
            return isControlledByLocalPlayer;
        }

        public void OnLootContainer(NetworkLootContainer container)
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return;
            }

            multiplayerActor.OnLootContainer(container);
        }

        public void OnDropItem(NetworkDropItem dropItem)
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return;
            }

            multiplayerActor.OnDropItem(dropItem);
        }

        public void OnChangeActiveHandEquipmentSet(NetworkActiveHandEquipmentSet set)
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return;
            }

            multiplayerActor.OnChangeActiveHandEquipmentSet(set);
        }

        public void OnInteractWithMapObjectOvertip(NetworkOvertip networkOvertip)
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return;
            }

            multiplayerActor.OnInteractWithMapObjectOvertip(networkOvertip);
        }

        public bool CanUnitJoinCombat(string unitId)
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return true;
            }

            return multiplayerActor.CanUnitJoinCombat(unitId);
        }

        public void OnPerceptionRoll(NetworkPerceptionCheck check)
        {
            if (!_multiplayerHost.IsActive)
            {
                return;
            }

            _multiplayerHost.OnPerceptionRoll(check);
        }

        public bool CanRollPerception(string unitId, string mapObjectId)
        {
            if (_multiplayerClient.IsActive)
            {
                var perceptionCheck = _gameInteractionService.ExecutionContext?.PerceptionCheck;
                if (perceptionCheck == null)
                {
                    return false;
                }

                return string.Equals(unitId, perceptionCheck.UnitId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(mapObjectId, perceptionCheck.MapObjectId, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        private bool ShouldRetrieveRoll(IMultiplayerActor multiplayerActor, object rule)
        {
            var gameMode = _gameInteractionService.CurrentGameMode;
            return multiplayerActor != null && IsMeaningfulRoll(gameMode, rule) && !multiplayerActor.IsDiceRollOwner(false);
        }

        private bool ShouldStoreRoll(IMultiplayerActor multiplayerActor, object rule)
        {
            var gameMode = _gameInteractionService.CurrentGameMode;
            return multiplayerActor != null && IsMeaningfulRoll(gameMode, rule) && multiplayerActor.IsDiceRollOwner(true);
        }

        private bool IsMeaningfulRoll(GameModeType gameModeType, object rule)
        {
            if (gameModeType == GameModeType.Dialog)
            {
                return rule is RuleSkillCheck or RuleSavingThrow;
            }
            else if (gameModeType == GameModeType.Cutscene || gameModeType == GameModeType.CutsceneGlobalMap)
            {
                return false;
            }

            return true;
        }

        private int? GetDiceRollId(NetworkDiceRollBase roll)
        {
            if (roll == null)
            {
                return null;
            }
            var rawId = roll.GetIdString();
            var id = _hashService.Murmur3(rawId);
            _logger.LogInformation("RollId has been generated. Id={rollId}, RollType={rollType}, RuleName={ruleName}, IdString={idString}", id, roll.RollType, roll.RuleName, rawId);
            return id;
        }

        private void SaveIntRollValue(IMultiplayerActor multiplayerActor, NetworkDiceRollBase networkDiceRoll, RuleRollDice ruleRollDice)
        {
            var rollType = networkDiceRoll.GetType().Name;
            var rollId = GetDiceRollId(networkDiceRoll);
            if (rollId == null)
            {
                _logger.LogWarning("Roll saving has been skipped due to unability to generate rollId. DiceType={diceType}, RollType={rollType}, InitiatorId={initiatorId}", ruleRollDice.GetType().Name, rollType, networkDiceRoll.InitiatorId);
                return;
            }

            var rollValue = new NetworkIntRollValue
            {
                RollHistory = [.. ruleRollDice.RollHistory ?? []],
                Value = ruleRollDice.m_Result
            };

            SaveRollValue(multiplayerActor, rollId.Value, rollValue);
        }

        private void SaveRollValue(IMultiplayerActor multiplayerActor, int rollId, RollValueBase rollValue)
        {
            var claimingList = multiplayerActor.GetOtherPlayers().Select(i => i.Id).ToList();
            _diceRollStorage.Add(rollId, claimingList, rollValue);
            _logger.LogInformation("Roll value has been stored. RollId={rollId}, RollValueType={rollValueType}, RollStringValue={rollValueString}, ClaimingListCount={claimingListCount}", rollId, rollValue.GetType().Name, rollValue, claimingList.Count);
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
                _logger.LogWarning("Unable to get damage roll id due to unhandled rule type. RuleType={ruleType} InitiatorId={initiatorId}", ruleCalculateDamage.Reason.Rule?.GetType().Name, ruleCalculateDamage.Initiator?.UniqueId);
                return null;
            }

            var rollId = GetDiceRollId(roll);
            if (rollId == null)
            {
                _logger.LogWarning("Unable to get damage roll id due to unability to generate rollId. InitiatorName={initiatorName}, InitiatorId={initiatorId}", ruleCalculateDamage.Initiator?.CharacterName, ruleCalculateDamage.Initiator?.UniqueId);
                return null;
            }

            return rollId.Value;
        }

        private T RetrieveRoll<T>(IMultiplayerActor multiplayerActor, NetworkDiceRollBase networkDiceRoll, UnitEntityData initiator)
            where T : RuleRollDice
        {
            try
            {
                var rollType = networkDiceRoll.GetType().Name;
                var rollId = GetDiceRollId(networkDiceRoll);
                if (rollId == null)
                {
                    _logger.LogWarning("Unable to retrieve roll due to null rollId. RollType={rollType}, InitiatorId={initiatorId}", rollType, initiator.UniqueId);
                    return null;
                }

                var roll = multiplayerActor.RetrieveRoll<NetworkIntRollValue>(rollId.Value, networkDiceRoll.InitiatorId);
                if (roll == null)
                {
                    _logger.LogCritical("Failed to acquire roll from remote player which guarantees desync in the game. RollId={rollId}, RollType={rollType}, InitiatorId={initiatorId}", rollId.Value, rollType, initiator.UniqueId);
                    _gameInteractionService.ShowModalMessage($"Failed to acquire {networkDiceRoll.RuleName} roll from remote player which guarantees desync in the game.");
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
                    _logger.LogError("Roll has been retrieved, but dicetype is not supported. DiceType={diceType}, RollId={rollId}", diceType, rollId.Value);
                    return null;
                }

                dice.RollHistory = [.. roll.RollHistory];

                _logger.LogInformation("{diceType} Roll has been acquired from another player. RollId={rollId}, Result={result}, RollType={rollType}, InitiatorId={initiatorId}",
                    diceType, rollId.Value, dice.Result, rollType, initiator.UniqueId);
                return dice;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unable to handle {MethodBase.GetCurrentMethod().Name}");
                throw;
            }
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
                AbilityId = ruleCheckConcentration.Reason?.Ability?.UniqueId,
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

        private HealDamageRoll CreateHealDamageRoll(NetworkDiceRollType diceRollType, RuleHealDamage ruleHealDamage, int unitsCount)
        {
            var roll = new HealDamageRoll(ruleHealDamage.Initiator.UniqueId, ruleHealDamage.GetType().Name, diceRollType, ruleHealDamage.Bonus)
            {
                AbilityId = ruleHealDamage.Reason.Ability.StickyTouch?.UniqueId ?? ruleHealDamage.Reason.Ability?.UniqueId,
                AbilitySchoolId = ruleHealDamage.Reason.Ability?.Spellbook?.Blueprint.name,
                TargetId = ruleHealDamage.Target?.UniqueId,
                UnitsCount = unitsCount,
                EmpowerModifier = ruleHealDamage.EmpowerModifier,
            };

            return roll;
        }

        private AbilityDamageRoll CreateAbilityUse(NetworkDiceRollType diceRollType, RuleCalculateDamage ruleCalculateDamage)
        {
            var roll = new AbilityDamageRoll(ruleCalculateDamage.Initiator.UniqueId, ruleCalculateDamage.ParentRule?.GetType().Name, diceRollType, ruleCalculateDamage.TotalBonusValue)
            {
                AbilityId = ruleCalculateDamage.Reason.Ability?.UniqueId,
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
            var roll = new AttackRoll(ruleAttackRoll.Initiator.UniqueId, ruleAttackRoll.GetType().Name, diceRollType, ruleAttackRoll.AttackBonus)
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
                AttacksCount = attackWithWeapon.AttacksCount,
            };

            return roll;
        }

        private InitiativeRoll CreateInitiativeRoll(NetworkDiceRollType diceRollType, RuleInitiativeRoll initiativeRoll)
        {
            var roll = new InitiativeRoll(initiativeRoll.Initiator.UniqueId, initiativeRoll.GetType().Name, diceRollType, initiativeRoll.Modifier);
            return roll;
        }

        private IMultiplayerActor GetMultiplayerActor()
        {
            return _multiplayerHost.IsActive ? _multiplayerHost
                : _multiplayerClient.IsActive ?
                    _multiplayerClient : null;
        }

        private void ShowEscMenuMultiplayerLobby()
        {
            _logger.LogInformation("Show lobby window");
            _lobbyWindow.Show(true);
        }

        private void ShowMultiplayerWindow()
        {
            _logger.LogInformation("Show Multiplayer window");
            _multiplayerWindow.Show(true);
        }

        private void OnLobbyCharacterOwnerChanged(int characterIndex, int playerIndex)
        {
            _logger.LogInformation("OnLobbyCharacterOwnerChanged. CharacterIndex={charIndex}, PlayerIndex={playerIndex}", characterIndex, playerIndex);
            _multiplayerHost.ChangeCharacterOwner(characterIndex, playerIndex);
        }
    }
}
