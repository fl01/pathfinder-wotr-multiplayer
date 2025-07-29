using System;
using System.Linq;
using System.Reflection;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Persistence;
using Kingmaker.GameModes;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Damage;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.Hashing;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Windows;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Abilities;
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

        public bool IsActive => _multiplayerClient.IsActive || _multiplayerHost.IsActive;

        public Multiplayer(
            ILogger<Multiplayer> logger,
            IUIFactory uiFactory,
            ILobbyWindowController lobbyWindowController,
            IMultiplayerHost multiplayerHost,
            IMultiplayerClient multiplayerClient,
            IHashService hashService,
            IDiceRollStorage diceRollStorage,
            IGameInteractionService gameInteractionService)
        {
            _logger = logger;
            Factory = uiFactory;
            _multiplayerHost = multiplayerHost;
            _multiplayerClient = multiplayerClient;
            _lobbyWindowController = lobbyWindowController;
            _hashService = hashService;
            _diceRollStorage = diceRollStorage;
            _gameInteractionService = gameInteractionService;
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
                if (multiplayerActor == null || multiplayerActor.ShouldStoreRoll(false))
                {
                    return true;
                }

                var rollId = GetDamageRollId(ruleCalculateDamage);
                if (rollId == null)
                {
                    _logger.LogWarning("Damage Roll retrieving has been skipped due to unability to generate rollId. InitiatorName={initiatorName}, InitiatorId={initiatorId}", ruleCalculateDamage.Initiator?.CharacterName, ruleCalculateDamage.Initiator?.UniqueId);
                    return true;
                }

                var networkRoll = multiplayerActor.RetrieveRoll<NetworkRollDamageValues>(rollId.Value, ruleCalculateDamage.Initiator.UniqueId);

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
                _logger.LogError(ex, $"Unable to handle {MethodBase.GetCurrentMethod().Name}");
                throw;
            }
        }

        public void OnAfterRuleCalculateDamageTrigger(RuleCalculateDamage ruleCalculateDamage)
        {
            try
            {
                var multiplayerActor = GetMultiplayerActor();
                if (multiplayerActor == null || !multiplayerActor.ShouldStoreRoll(true))
                {
                    return;
                }

                var rollId = GetDamageRollId(ruleCalculateDamage);
                if (rollId == null)
                {
                    _logger.LogWarning("Damage Roll saving has been skipped due to unability to generate rollId. InitiatorName={initiatorName}, InitiatorId={initiatorId}", ruleCalculateDamage.Initiator?.CharacterName, ruleCalculateDamage.Initiator?.UniqueId);
                    return;
                }

                var rollValue = new NetworkRollDamageValues
                {
                    Value = [..ruleCalculateDamage.CalculatedDamage.Select(x => new NetworkRollDamageRoll
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
                _logger.LogError(ex, $"Unable to handle {MethodBase.GetCurrentMethod().Name}");
                throw;
            }
        }

        public void OnAfterRuleRollDiceTrigger(RuleRollDice ruleRollDice)
        {
            try
            {
                var multiplayerActor = GetMultiplayerActor();
                if (multiplayerActor == null || !multiplayerActor.ShouldStoreRoll(true))
                {
                    return;
                }

                // TODO: this is generic handler, but attacks are handled in a different way. Consider replacing generic with specific handlers (RulePartyStatCheck, RuleInitiativeRoll)
                if (ruleRollDice.Reason.Rule is RuleAttackWithWeapon)
                {
                    return;
                }

                var rollId = GetDiceRollId(ruleRollDice.Reason, NetworkDiceRollType.Hit);
                if (rollId == null)
                {
                    _logger.LogWarning("Roll saving has been skipped due to unability to generate rollId. ReasonRuleType={reasonRuleType}, InitiatorName={initiatorName}, InitiatorId={initiatorId}", ruleRollDice.Reason.Rule?.GetType().Name, ruleRollDice.Initiator.CharacterName, ruleRollDice.Initiator.UniqueId);
                    return;
                }

                var rollValue = new NetworkRollIntValue
                {
                    RollHistory = [.. ruleRollDice.RollHistory ?? []],
                    Value = ruleRollDice.m_Result
                };

                SaveRollValue(multiplayerActor, rollId.Value, rollValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unable to handle {MethodBase.GetCurrentMethod().Name}");
                throw;
            }
        }

        public bool OnBeforeRuleRollDiceTrigger(RuleRollDice ruleRollDice)
        {
            try
            {
                var multiplayerActor = GetMultiplayerActor();
                if (multiplayerActor == null || multiplayerActor.ShouldStoreRoll(false))
                {
                    return true;
                }

                // TODO: this is generic handler, but attacks are handled in a different way. Consider replacing generic with specific handlers (RulePartyStatCheck, RuleInitiativeRoll)
                if (ruleRollDice.Reason.Rule is RuleAttackWithWeapon)
                {
                    return true;
                }

                var rollId = GetDiceRollId(ruleRollDice.Reason, NetworkDiceRollType.Hit);
                if (rollId == null)
                {
                    _logger.LogWarning("Roll retrieving has been skipped due to unability to generate rollId. ReasonRuleType={reasonRuleType} InitiatorName={initiatorName}, InitiatorId={initiatorId}", ruleRollDice.Reason.Rule?.GetType().Name, ruleRollDice.Initiator.CharacterName, ruleRollDice.Initiator.UniqueId);
                    return true;
                }

                var roll = multiplayerActor.RetrieveRoll<NetworkRollIntValue>(rollId.Value, ruleRollDice.Initiator.UniqueId);
                if (roll == null)
                {
                    _logger.LogCritical("Failed to acquire roll from remote player which guarantees desync in the game. RollType={rollType}", ruleRollDice.Reason.Rule?.GetType().Name);
                    _gameInteractionService.ShowModalMessage($"Failed to acquire roll from remote player which guarantees desync in the game.");
                    return true;
                }

                ruleRollDice.m_Result = roll.Value;
                ruleRollDice.RollHistory = [.. roll.RollHistory];

                _logger.LogInformation("Roll result has been acquired from another player. RollId={rollId}, Result={result}, RollType={rollType}", rollId.Value, ruleRollDice.Result, ruleRollDice.Reason.Rule?.GetType().Name);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unable to handle {MethodBase.GetCurrentMethod().Name}");
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

                if (multiplayerActor.ShouldStoreRoll(false))
                {
                    var value = new NetworkRollIntValue { Value = result };
                    SaveRollValue(multiplayerActor, rollId.Value, value);
                    return result;
                }

                var d20 = RetrieveD20Roll(multiplayerActor, roll, ruleHealDamage.Initiator);
                return d20.Result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unable to handle {MethodBase.GetCurrentMethod().Name}");
                throw;
            }
        }

        public bool OnBeforeRuleAttackRoll(RuleAttackRoll ruleAttackRoll)
        {
            try
            {
                var multiplayerActor = GetMultiplayerActor();
                if (multiplayerActor == null || multiplayerActor.ShouldStoreRoll(false))
                {
                    return true;
                }

                var roll = CreateAttackRoll(NetworkDiceRollType.Hit, ruleAttackRoll);
                var d20 = RetrieveD20Roll(multiplayerActor, roll, ruleAttackRoll.Initiator);
                if (d20 == null)
                {
                    return true;
                }

                ruleAttackRoll.D20 = d20;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unable to handle {MethodBase.GetCurrentMethod().Name}");
                throw;
            }
        }

        public void OnAfterRuleAttackRollTrigger(RuleAttackRoll ruleAttackRoll)
        {
            try
            {
                var multiplayerActor = GetMultiplayerActor();
                if (multiplayerActor == null || !multiplayerActor.ShouldStoreRoll(false) || ruleAttackRoll.D20 == null)
                {
                    return;
                }

                var roll = CreateAttackRoll(NetworkDiceRollType.Hit, ruleAttackRoll);
                SaveIntRollValue(multiplayerActor, roll, ruleAttackRoll.D20);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unable to handle {MethodBase.GetCurrentMethod().Name}");
                throw;
            }
        }

        public void OnAfterRuleSavingThrowTrigger(RuleSavingThrow ruleSavingThrow)
        {
            try
            {
                var multiplayerActor = GetMultiplayerActor();
                if (multiplayerActor == null || !multiplayerActor.ShouldStoreRoll(false))
                {
                    return;
                }

                var savingThrow = CreateSavingThrowRoll(NetworkDiceRollType.Hit, ruleSavingThrow);
                SaveIntRollValue(multiplayerActor, savingThrow, ruleSavingThrow.D20);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unable to handle {MethodBase.GetCurrentMethod().Name}");
                throw;
            }
        }

        public void OnBeforeRuleSavingThrowRoll(RuleSavingThrow ruleSavingThrow)
        {
            try
            {
                var multiplayerActor = GetMultiplayerActor();
                if (multiplayerActor == null || multiplayerActor.ShouldStoreRoll(false))
                {
                    return;
                }

                var savingThrow = CreateSavingThrowRoll(NetworkDiceRollType.Hit, ruleSavingThrow);
                ruleSavingThrow.D20 = RetrieveD20Roll(multiplayerActor, savingThrow, ruleSavingThrow.Initiator);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unable to handle {MethodBase.GetCurrentMethod().Name}");
                throw;
            }
        }

        public void OnAfterCueShow(string dialogName, string cueName, bool hasSystemAnswer)
        {
            var multiplayerActor = GetMultiplayerActor();
            multiplayerActor.OnAfterCueShow(dialogName, cueName, hasSystemAnswer);
        }

        public bool OnBeforeSelectDialogAnswer(string dialogName, string cueName, string answerName, bool isExitAnswer, string manualUnitSelectionId)
        {
            // host - check if everyone witnessed current cue
            // client - skip execution if triggered by user himself, send notification to host => mark answer on host side
            var multiplayerActor = GetMultiplayerActor();
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
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return true;
            }

            return multiplayerActor.CanInitializeCombat();
        }

        public bool CanTickCombatController()
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return true;
            }

            return multiplayerActor.CanContinueCombat();
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
            _logger.LogInformation("Force load game. Save={saveLocation}", savePath);
            multiplayerActor.ForceLoadGame(savePath);
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

            return _gameInteractionService.GetActionsState();
        }

        private int? GetDiceRollId(RuleReason ruleReason, NetworkDiceRollType networkDiceRollType)
        {
            NetworkDiceRollBase roll = ruleReason.Rule switch
            {
                RulePartyStatCheck rulePartyStatCheck => CreatePartyStatCheckRoll(networkDiceRollType, rulePartyStatCheck),
                RuleInitiativeRoll ruleInitiativeRoll => CreateInitiativeRoll(networkDiceRollType, ruleInitiativeRoll),
                _ => null,
            };

            return GetDiceRollId(roll);
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

        private void SaveIntRollValue(IMultiplayerActor multiplayerActor, NetworkDiceRollBase networkDiceRoll, RuleRollD20 ruleRollD20)
        {
            var rollType = networkDiceRoll.GetType().Name;
            var rollId = GetDiceRollId(networkDiceRoll);
            if (rollId == null)
            {
                _logger.LogWarning("Roll saving has been skipped due to unability to generate rollId. RollType={rollType}, InitiatorId={initiatorId}", rollType, networkDiceRoll.InitiatorId);
                return;
            }

            var rollValue = new NetworkRollIntValue
            {
                RollHistory = [.. ruleRollD20.RollHistory ?? []],
                Value = ruleRollD20.m_Result
            };

            SaveRollValue(multiplayerActor, rollId.Value, rollValue);
        }

        private void SaveRollValue(IMultiplayerActor multiplayerActor, int rollId, RollValueBase rollValue)
        {
            var claimingList = multiplayerActor.GetOtherPlayers().Select(i => i.Id).ToList();
            var playerId = multiplayerActor.GetLocalPlayerId();
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

        private RuleRollD20 RetrieveD20Roll(IMultiplayerActor multiplayerActor, NetworkDiceRollBase networkDiceRoll, UnitEntityData initiator)
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

                var roll = multiplayerActor.RetrieveRoll<NetworkRollIntValue>(rollId.Value, networkDiceRoll.InitiatorId);
                if (roll == null)
                {
                    _logger.LogCritical("Failed to acquire roll from remote player which guarantees desync in the game. RollId={rollId}, RollType={rollType}, InitiatorId={initiatorId}", rollId.Value, rollType, initiator.UniqueId);
                    _gameInteractionService.ShowModalMessage($"Failed to acquire {networkDiceRoll.RuleName} roll from remote player which guarantees desync in the game.");
                    return null;
                }

                var d20 = RuleRollD20.FromInt(initiator, roll.Value);
                d20.RollHistory = [.. roll.RollHistory];

                _logger.LogInformation("D20 Roll has been acquired from another player. RollId={rollId}, Result={result}, RollType={rollType}, InitiatorId={initiatorId}", rollId.Value, d20.Result, rollType, initiator.UniqueId);
                return d20;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unable to handle {MethodBase.GetCurrentMethod().Name}");
                throw;
            }
        }

        private HealDamageRoll CreateHealDamageRoll(NetworkDiceRollType diceRollType, RuleHealDamage ruleHealDamage, int unitsCount)
        {
            var roll = new HealDamageRoll(ruleHealDamage.Initiator.UniqueId, ruleHealDamage.GetType().Name, diceRollType, ruleHealDamage.Bonus)
            {
                AbilityId = ruleHealDamage.Reason.Ability?.UniqueId,
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
            var roll = new SavingThrowRoll(ruleSavingThrow.Initiator.UniqueId, ruleSavingThrow.GetType().Name, diceRollType, ruleSavingThrow.TotalBonusValue)
            {
                StatType = ruleSavingThrow.StatType,
                ReasonAbilityName = ruleSavingThrow.Reason?.Ability?.NameForAcronym,
                ReasonCasterId = ruleSavingThrow.Reason?.Caster?.UniqueId,
                DifficultyClass = ruleSavingThrow.DifficultyClass,
            };

            return roll;
        }

        private PartyStatCheckRoll CreatePartyStatCheckRoll(NetworkDiceRollType diceRollType, RulePartyStatCheck partyStatCheck)
        {
            var roll = new PartyStatCheckRoll(partyStatCheck.Initiator.UniqueId, partyStatCheck.GetType().Name, diceRollType, partyStatCheck.TotalBonusValue)
            {
                DifficultyClass = partyStatCheck.DifficultyClass,
                StatType = partyStatCheck.StatType
            };

            return roll;
        }

        private AttackRoll CreateAttackRoll(NetworkDiceRollType diceRollType, RuleAttackRoll ruleAttackRoll)
        {
            var roll = new AttackRoll(ruleAttackRoll.Initiator.UniqueId, ruleAttackRoll.GetType().Name, diceRollType, ruleAttackRoll.AttackBonus)
            {
                AttackType = ruleAttackRoll.AttackType,
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
                IsCriticalRoll = attackWithWeapon.AttackRoll.IsCriticalRoll,
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
