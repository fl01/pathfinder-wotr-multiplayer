using System.Collections.Generic;
using System.Linq;
using Kingmaker.EntitySystem.Persistence;
using Kingmaker.GameModes;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Damage;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Windows;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Rolls;
using WOTRMultiplayer.UI.Lobby;

namespace WOTRMultiplayer.MP
{
    public class Multiplayer : IMultiplayer
    {
        private IMultiplayerWindow _multiplayerWindow;
        private ILobbyWindow _lobbyWindow;

        private readonly ILobbyWindowController _lobbyWindowController;
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
            IDiceRollStorage diceRollStorage,
            IGameInteractionService gameInteractionService)
        {
            _logger = logger;
            Factory = uiFactory;
            _multiplayerHost = multiplayerHost;
            _multiplayerClient = multiplayerClient;
            _lobbyWindowController = lobbyWindowController;
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
            _diceRollStorage.Reset();

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

        public bool CanControlCharacter(bool original, string unitId)
        {
            // no need to possibly override value if this character is not controllable at all
            if (!original)
            {
                {
                    return original;
                }
            }

            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return false;
            }

            return multiplayerActor.CanControlCharacter(unitId);
        }

        public bool StartGameMode(GameModeType type)
        {
            var allowedToRun = type != GameModeType.EscMode && type != GameModeType.FullScreenUi;
            _logger.LogInformation("Trying to start GameModeType. Mode={mode}, AllowedToRun={allowedToRun}", type.Name, allowedToRun);

            if (type == GameModeType.Pause)
            {
                var multiplayerActor = GetMultiplayerActor();
                multiplayerActor.Pause();
            }

            return allowedToRun;
        }

        public bool StopGameMode(GameModeType type)
        {
            _logger.LogInformation("Trying to stop GameModeType. Mode={mode}", type.Name);

            if (type == GameModeType.Pause)
            {
                var multiplayerActor = GetMultiplayerActor();
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

                var rule = ruleCalculateDamage.Reason.Rule;
                var ruleType = rule.GetType().Name;
                var combatRound = multiplayerActor.GetCombatRound();
                var roll = CreateNetworkDiceRoll(rule, combatRound);
                if (roll == null)
                {
                    _logger.LogWarning("Damage Roll retrieving has been skipped. Type={ruleType}, InitiatorName={initiatorName}, InitiatorId={initiatorId}", ruleType, ruleCalculateDamage.Initiator?.CharacterName, ruleCalculateDamage.Initiator?.UniqueId);
                    return true;
                }

                _logger.LogInformation("Retrieving Damage roll. Type={type}, IdString={id}", roll.GetType().Name, roll.GetIdString());

                var networkDiceRollId = _diceRollStorage.GetUniqueId(roll);
                var networkRoll = multiplayerActor.RetrieveRoll(networkDiceRollId, ruleCalculateDamage.Initiator.UniqueId);

                if (networkRoll == null)
                {
                    _logger.LogCritical("Failed to acquire damage roll from remote player which guarantees desync in the game. RollId={rollId} RuleType={rollType}", networkDiceRollId, ruleType);
                    _gameInteractionService.ShowModalMessage($"Failed to acquire roll from remote player which guarantees desync in the game. RollType={ruleType}");
                    return true;
                }
                var bundles = ruleCalculateDamage.DamageBundle.ToList();
                if (networkRoll.DamageValues.Count != bundles.Count)
                {
                    _logger.LogCritical("Network damage contains invalid number of damage values. RollId={rollId} RuleType={rollType}, ExpectedCount={expectedCount}, ActualCount={actualCount}", networkDiceRollId, ruleType, bundles.Count, networkRoll.DamageValues.Count);
                    _gameInteractionService.ShowModalMessage($"Network damage contains invalid number of damage values which guarantees desync in the game. RollType={ruleType}");
                    return true;
                }

                for (int i = 0; i < bundles.Count; i++)
                {
                    var damage = bundles[i];
                    var networkDamageValue = networkRoll.DamageValues[i];
                    var damageValue = new DamageValue(damage, networkDamageValue.ValueWithoutReduction, networkDamageValue.RollAndBonusValue, networkDamageValue.RollResult, networkDamageValue.TacticalCombatDRModifier);
                    damageValue.Source.MaximumValue = networkDamageValue.MaximumDamage;
                    ruleCalculateDamage.CalculatedDamage.Add(damageValue);
                }

                _logger.LogInformation("Damage roll result has been acquired from another player. RollId={rollId}, DamageValuesCount={damageValuesCount}", networkDiceRollId, ruleCalculateDamage.CalculatedDamage.Count);
                return false;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Unable to handle OnBeforeRuleCalculateDamageTrigger");
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

                var combatRound = multiplayerActor.GetCombatRound();
                var roll = CreateNetworkDiceRoll(ruleCalculateDamage.Reason.Rule, combatRound);
                var rollType = ruleCalculateDamage.Reason?.Rule?.GetType().Name;
                if (roll == null)
                {
                    _logger.LogWarning("Damage Roll saving has been skipped. Type={rollType}, InitiatorName={initiatorName}, InitiatorId={initiatorId}", rollType, ruleCalculateDamage.Initiator?.CharacterName, ruleCalculateDamage.Initiator?.UniqueId);
                    return;
                }

                var rollUniqueId = _diceRollStorage.GetUniqueId(roll);
                var playerId = multiplayerActor.GetLocalPlayerId();
                var storedDiceRoll = _diceRollStorage.Get(rollUniqueId, playerId, ensureCompleted: false);
                if (storedDiceRoll == null)
                {
                    _logger.LogError("Unable to attach damage values to missing roll. RuleType={ruleType}, RollUniqueId={rollUniqueId}", rollType, rollUniqueId);
                    return;
                }

                if (storedDiceRoll.DamageValues.Count > 0)
                {
                    _logger.LogWarning("Damage values already exist. RuleType={ruleType}, RollUniqueId={rollUniqueId}", rollType, rollUniqueId);
                }

                storedDiceRoll.DamageValues = [.. ruleCalculateDamage.CalculatedDamage.Select(x => new NetworkDamageValueRoll
                {
                    MaximumDamage = x.Source.MaximumValue,
                    RollAndBonusValue = x.RollAndBonusValue,
                    RollResult = x.RollResult,
                    TacticalCombatDRModifier = x.TacticalCombatDRModifier,
                    ValueWithoutReduction = x.ValueWithoutReduction
                })];

                _logger.LogInformation("Damage values have been attached. RollId={rollId}, RollType={rollType}, DamageValuesCount={damageValuesCount}", rollUniqueId, rollType, storedDiceRoll.DamageValues.Count);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Unable to handle OnAfterRuleCalculateDamageTrigger");
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

                var combatRound = multiplayerActor.GetCombatRound();
                var roll = CreateNetworkDiceRoll(ruleRollDice.Reason?.Rule, ruleRollDice.RollHistory, ruleRollDice.Result, combatRound);

                var rollType = ruleRollDice.Reason?.Rule?.GetType().Name;
                if (roll == null)
                {
                    _logger.LogWarning("Roll saving has been skipped. Type={rollType}, InitiatorName={initiatorName}, InitiatorId={initiatorId}", rollType, ruleRollDice.Initiator?.CharacterName, ruleRollDice.Initiator?.UniqueId);
                    return;
                }

                _logger.LogInformation("Saving roll. Type={type}, IdString={id}", roll.GetType().Name, roll.GetIdString());

                if (!_diceRollStorage.Save(roll))
                {
                    var message = $"Roll has not been saved which guarantees to cause desync in the game. RollType={rollType}";
                    _logger.LogCritical(message);
                    _gameInteractionService.ShowModalMessage(message);
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Unable to handle OnAfterRuleRollDiceTrigger");
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

                var initiatorId = ruleRollDice.Initiator.UniqueId;
                var combatRound = multiplayerActor.GetCombatRound();
                var rollType = ruleRollDice.Reason?.Rule?.GetType().Name;

                var networkDiceRoll = CreateNetworkDiceRoll(ruleRollDice.Reason?.Rule, ruleRollDice.RollHistory, ruleRollDice.Result, combatRound);
                if (networkDiceRoll == null)
                {
                    _logger.LogWarning("Roll retrieving has been skipped. Type={rollType}, InitiatorName={initiatorName}, InitiatorId={initiatorId}", rollType, ruleRollDice.Initiator?.CharacterName, ruleRollDice.Initiator?.UniqueId);
                    return true;
                }

                _logger.LogInformation("Retrieving roll. Type={type}, IdString={id}", networkDiceRoll.GetType().Name, networkDiceRoll.GetIdString());

                var networkDiceRollId = _diceRollStorage.GetUniqueId(networkDiceRoll);

                var roll = multiplayerActor.RetrieveRoll(networkDiceRollId, initiatorId);
                if (roll == null)
                {
                    _logger.LogCritical("Failed to acquire roll from remote player which guarantees desync in the game. RollType={rollType}", rollType);
                    _gameInteractionService.ShowModalMessage($"Failed to acquire roll from remote player which guarantees desync in the game. RollType={rollType}");
                    return true;
                }

                ruleRollDice.m_Result = roll.Result;
                if (ruleRollDice.RollHistory?.Count > 0)
                {
                    _logger.LogWarning("Roll history is not empty. RollId={rollId}, RollType={rollType}", networkDiceRollId, rollType);
                }

                ruleRollDice.RollHistory = [.. roll.RollHistory];

                _logger.LogInformation("Roll result has been acquired from another player. RollId={rollId}, Result={result}, RollType={rollType}", networkDiceRollId, ruleRollDice.Result, rollType);
                return false;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Unable to handle OnBeforeRuleRollDiceTrigger");
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

            return multiplayerActor.CanControlCharacter(unitId);
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

        public void OnClickWithSelectedAbility(NetworkClick click)
        {
            var multiplayerActor = GetMultiplayerActor();
            if (multiplayerActor == null)
            {
                return;
            }

            multiplayerActor.OnClickWithSelectedAbility(click);
        }

        private NetworkDiceRoll CreateNetworkDiceRoll(RulebookEvent rulebookEvent, int combatRound)
        {
            return CreateNetworkDiceRoll(rulebookEvent, [], 0, combatRound);
        }

        private NetworkDiceRoll CreateNetworkDiceRoll(RulebookEvent rulebookEvent, List<int> rollHistory, int result, int combatRound)
        {
            NetworkDiceRoll roll = rulebookEvent switch
            {
                RulePartyStatCheck rulePartyStatCheck => CreatePartyStatCheckRoll(rollHistory, result, rulePartyStatCheck),
                RuleInitiativeRoll ruleInitiativeRoll => CreateInitiativeRoll(rollHistory, result, ruleInitiativeRoll),
                RuleAttackWithWeapon ruleAttackWithWeapon => CreateAttackWithWeaponRoll(rollHistory, result, ruleAttackWithWeapon, combatRound),
                _ => null
            };

            return roll;
        }

        private PartyStatCheckRoll CreatePartyStatCheckRoll(List<int> rollHistory, int result, RulePartyStatCheck rulePartySkillCheck)
        {
            var roll = new PartyStatCheckRoll
            {
                InitiatorId = rulePartySkillCheck.Initiator.UniqueId,
                Result = result,
                RollHistory = [.. rollHistory ?? []],
                RuleRollName = rulePartySkillCheck.Reason.Name,
                RuleRollType = rulePartySkillCheck.GetType().Name,
                DifficultyClass = rulePartySkillCheck.DifficultyClass,
                StatType = rulePartySkillCheck.StatType
            };

            return roll;
        }

        private AttackWithWeaponRoll CreateAttackWithWeaponRoll(List<int> rollHistory, int result, RuleAttackWithWeapon ruleAttackWithWeapon, int combatRound)
        {
            var roll = new AttackWithWeaponRoll
            {
                InitiatorId = ruleAttackWithWeapon.Initiator.UniqueId,
                RuleRollName = ruleAttackWithWeapon.Weapon.Name,
                RuleRollType = ruleAttackWithWeapon.GetType().Name,
                TotalModifiersBonus = ruleAttackWithWeapon.AttackRoll.AttackBonus,

                Result = result,
                RollHistory = [.. rollHistory ?? []],
                CombatRound = combatRound,
                AttackNumber = ruleAttackWithWeapon.AttackNumber,
                IsAttackOfOpportunity = ruleAttackWithWeapon.IsAttackOfOpportunity,
                TargetId = ruleAttackWithWeapon.Target.UniqueId,
                ExtraAttack = ruleAttackWithWeapon.ExtraAttack,
                IsFirstAttack = ruleAttackWithWeapon.IsFirstAttack,
                AttacksCount = ruleAttackWithWeapon.AttacksCount,
                IsCriticalRoll = ruleAttackWithWeapon.AttackRoll.IsCriticalRoll,

                IsHit = ruleAttackWithWeapon.AttackRoll.IsHit
            };

            return roll;
        }

        private InitiativeRoll CreateInitiativeRoll(List<int> rollHistory, int result, RuleInitiativeRoll ruleInitiativeRoll)
        {
            var roll = new InitiativeRoll
            {
                InitiatorId = ruleInitiativeRoll.Initiator.UniqueId,
                Result = result,
                RollHistory = [.. rollHistory ?? []],
                RuleRollName = ruleInitiativeRoll.Reason.Name,
                RuleRollType = ruleInitiativeRoll.GetType().Name,
                TotalModifiersBonus = ruleInitiativeRoll.Modifier
            };

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
