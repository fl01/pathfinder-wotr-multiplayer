using System.Numerics;
using Kingmaker.Controllers.Clicks.Handlers;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.GameModes;
using Kingmaker.RuleSystem.Rules;
using Microsoft.Extensions.Logging;
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
        private readonly IDiceRollStorage _rollStorage;
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
            IDiceRollStorage rollStorage)
        {
            _logger = logger;
            Factory = uiFactory;
            _multiplayerHost = multiplayerHost;
            _multiplayerClient = multiplayerClient;
            _lobbyWindowController = lobbyWindowController;
            _rollStorage = rollStorage;
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
            _rollStorage.Reset();

        }

        public void InitializeEscMenuLobbyWindow(InitializeEscMenuLobbyWindowContext context)
        {
            _logger.LogInformation("Creating Esc menu multiplayer lobby window");
            _lobbyWindow = Factory.InitializeEscMenuLobbyWindow(context, _multiplayerHost.IsActive, ShowEscMenuMultiplayerLobby);

            _lobbyWindow.NetworkGame = GetNetworkGame;
            _lobbyWindow.AssignLobbyController(_lobbyWindowController);

            _lobbyWindowController.OnCharacterOwnerChanged = OnLobbyCharacterOwnerChanged;
        }

        public void MoveCharacter(UnitEntityData unit, ClickGroundHandler.CommandSettings settings)
        {
            var destination = new Vector3(settings.Destination.x, settings.Destination.y, settings.Destination.z);
            var multiplayerParticipant = GetMultiplayerParticipant();
            multiplayerParticipant.MoveCharacter(unit.CharacterName, destination, settings.Delay, settings.Orientation);
        }

        public bool CanControlCharacter(string characterName)
        {
            var multiplayerParticipant = GetMultiplayerParticipant();
            return multiplayerParticipant.CanControlCharacter(characterName);
        }

        public bool StartGameMode(GameModeType type)
        {
            var allowedToRun = type != GameModeType.EscMode && type != GameModeType.FullScreenUi;
            _logger.LogInformation("Trying to start GameModeType. Mode={mode}, AllowedToRun={allowedToRun}", type.Name, allowedToRun);

            if (type == GameModeType.Pause)
            {
                var multiplayerParticipant = GetMultiplayerParticipant();
                multiplayerParticipant.Pause();
            }

            return allowedToRun;
        }

        public bool StopGameMode(GameModeType type)
        {
            _logger.LogInformation("Trying to stop GameModeType. Mode={mode}", type.Name);

            if (type == GameModeType.Pause)
            {
                var multiplayerParticipant = GetMultiplayerParticipant();
                multiplayerParticipant.Unpause();
            }

            return true;
        }

        public bool CanLeaveArea()
        {
            return !_multiplayerClient.IsActive;
        }

        /// <summary>
        /// host - store roll
        /// client - ignore
        /// </summary>
        /// <param name="ruleRollDice"></param>
        public void OnAfterRuleRollDiceTrigger(RuleRollDice ruleRollDice)
        {
            if (!_multiplayerHost.IsActive)
            {
                return;
            }

            switch (ruleRollDice.Reason.Rule)
            {
                case RulePartyStatCheck rulePartyStatCheck:
                    var partyStatCheckRoll = CreatePartyStatCheckRoll(ruleRollDice, rulePartyStatCheck);
                    _rollStorage.Add(partyStatCheckRoll);
                    break;
                case RuleInitiativeRoll ruleInitiativeRoll:
                    var initiativeRoll = CreateInitiativeRoll(ruleRollDice, ruleInitiativeRoll);
                    _rollStorage.Add(initiativeRoll);
                    break;
                case RuleAttackWithWeapon ruleAttackWithWeapon:
                    var combatRound = _multiplayerHost.GetCombatRound();
                    var attackWithWeaponRoll = CreateAttackWithWeaponRoll(ruleRollDice, ruleAttackWithWeapon, combatRound);
                    _rollStorage.Add(attackWithWeaponRoll);
                    break;
                case RuleRollD20:
                default:
                    _logger.LogWarning("Roll saving has been skipped. Type={rollType}, InitiatorName={initiatorName}, InitiatorId={initiatorId}", ruleRollDice.Reason.Rule.GetType().Name, ruleRollDice.Initiator?.CharacterName, ruleRollDice.Initiator?.UniqueId);
                    break;
            }
        }

        /// <summary>
        /// host - ignore
        /// client - get roll from host
        /// </summary>
        /// <param name="ruleRollDice"></param>
        /// <returns></returns>
        public bool OnBeforeRuleRollDiceTrigger(RuleRollDice ruleRollDice)
        {
            if (!_multiplayerClient.IsActive)
            {
                return true;
            }

            switch (ruleRollDice.Reason.Rule)
            {
                case RulePartyStatCheck rulePartyStatCheck:
                    var partyRoll = CreatePartyStatCheckRoll(ruleRollDice, rulePartyStatCheck);
                    var partyRollId = _rollStorage.GetUniqueId(partyRoll);
                    var hostPartyRoll = _multiplayerClient.GetHostRoll(partyRollId);
                    if (hostPartyRoll == null)
                    {
                        _logger.LogCritical("RulePartyStatCheck is not available at host => it will be rolled by the game");
                        return true;
                    }

                    ruleRollDice.m_Result = hostPartyRoll.Result;
                    if (ruleRollDice.RollHistory?.Count > 0)
                    {
                        _logger.LogWarning("RulePartyStatCheck history is not empty. RollId={rollId}", partyRollId);
                    }

                    ruleRollDice.RollHistory = [.. hostPartyRoll.RollHistory];
                    _logger.LogInformation("RulePartyStatCheck results has been acquired from host. RollId={rollId}, Result={result}", partyRollId, ruleRollDice.Result);
                    return false;
                case RuleInitiativeRoll ruleInitiativeRoll:
                    var initiativeRoll = CreateInitiativeRoll(ruleRollDice, ruleInitiativeRoll);
                    var initiativeRollId = _rollStorage.GetUniqueId(initiativeRoll);
                    var hostInitiativeRoll = _multiplayerClient.GetHostRoll(initiativeRollId);
                    if (hostInitiativeRoll == null)
                    {
                        _logger.LogCritical("RuleInitiativeRoll is not available at host => it will be rolled by the game");
                        return true;
                    }

                    ruleRollDice.m_Result = hostInitiativeRoll.Result;
                    if (ruleRollDice.RollHistory?.Count > 0)
                    {
                        _logger.LogWarning("RuleInitiativeRoll history is not empty. RollId={rollId}", initiativeRollId);
                    }

                    ruleRollDice.RollHistory = [.. hostInitiativeRoll.RollHistory];
                    _logger.LogInformation("RuleInitiativeRoll results has been acquired from host. RollId={rollId}, Result={result}, Modifier={totalBonus}", initiativeRollId, ruleRollDice.Result, ruleInitiativeRoll.Modifier);
                    return false;
                case RuleRollD20:
                default:
                    _logger.LogWarning("Roll retrieving has been skipped. Type={rollType}, InitiatorName={initiatorName}, InitiatorId={initiatorId}", ruleRollDice.Reason.Rule.GetType().Name, ruleRollDice.Initiator?.CharacterName, ruleRollDice.Initiator?.UniqueId);
                    break;
            }

            return true;
        }

        public void OnAfterCueShow(string dialogName, string cueName, bool hasSystemAnswer)
        {
            var participant = GetMultiplayerParticipant();
            participant.OnAfterCueShow(dialogName, cueName, hasSystemAnswer);
        }

        public bool OnBeforeSelectDialogAnswer(string dialogName, string cueName, string answerName, bool isExitAnswer, string manualUnitSelectionId)
        {
            // host - check if everyone witnessed current cue
            // client - skip execution if triggered by user himself, send notification to host => mark answer on host side
            var participant = GetMultiplayerParticipant();
            var shouldContinueExecution = participant.OnBeforeSelectDialogAnswer(dialogName, cueName, answerName, isExitAnswer, manualUnitSelectionId);
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
            _logger.LogInformation("Start dialog. DialogueName={dialogName},  TargetUnitId={targetUnitId}, InitiatorUnitId={initiatorUnitId}, MapObjectId={mapObjectId}, SpeakerKey={speakerKey}",
                dialogName, targetUnitId, initiatorUnitId, mapObjectId, speakerKey);

            // host - start dialog & send notification to clients
            // clients - request dialog from host
            var participant = GetMultiplayerParticipant();
            return participant.StartDialog(dialogName, targetUnitId, initiatorUnitId, mapObjectId, speakerKey);
        }

        public bool CanTickUnitCombatPrepareController()
        {
            var participant = GetMultiplayerParticipant();
            // host - always true
            // client - block until confirmation from host
            return participant.CanInitializeCombat();
        }

        public bool CanTickCombatController()
        {
            var participant = GetMultiplayerParticipant();
            // host - confirm initialization with clients
            // client - block until confirmation from host
            return participant.CanContinueCombat();
        }

        public bool OnBeforeStartTurn(string unitId)
        {
            var participant = GetMultiplayerParticipant();
            return participant.OnBeforeStartTurn(unitId);
        }

        public bool OnBeforeEndTurn(string unitId)
        {
            var participant = GetMultiplayerParticipant();
            return participant.OnBeforeEndTurn(unitId);
        }

        private PartyStatCheckRoll CreatePartyStatCheckRoll(RuleRollDice ruleRollDice, RulePartyStatCheck rulePartySkillCheck)
        {
            var roll = new PartyStatCheckRoll
            {
                InitiatorId = ruleRollDice.Initiator?.UniqueId,
                Result = ruleRollDice.Result,
                ResultOverride = ruleRollDice.ResultOverride,
                RollHistory = [.. ruleRollDice.RollHistory ?? []],
                RuleRollName = ruleRollDice.Reason.Name,
                RuleRollType = ruleRollDice.Reason.Rule.GetType().Name,
                DiceType = ruleRollDice.DiceFormula.Dice,
                DifficultyClass = rulePartySkillCheck.DifficultyClass,
                StatType = rulePartySkillCheck.StatType
            };

            return roll;
        }

        private AttackWithWeaponRoll CreateAttackWithWeaponRoll(RuleRollDice ruleRollDice, RuleAttackWithWeapon ruleAttackWithWeapon, int combatRound)
        {
            var roll = new AttackWithWeaponRoll
            {
                InitiatorId = ruleRollDice.Initiator?.UniqueId,
                Result = ruleRollDice.Result,
                ResultOverride = ruleRollDice.ResultOverride,
                RollHistory = [.. ruleRollDice.RollHistory ?? []],
                RuleRollName = ruleRollDice.Reason.Name,
                RuleRollType = ruleRollDice.Reason.Rule.GetType().Name,
                DiceType = ruleRollDice.DiceFormula.Dice,
                CombatRound = combatRound,
                TotalModifiersBonus = ruleAttackWithWeapon.AttackRoll.AttackBonus,
                AttackNumber = ruleAttackWithWeapon.AttackNumber,
                IsAttackOfOpportunity = ruleAttackWithWeapon.IsAttackOfOpportunity,
                TargetId = ruleAttackWithWeapon.Target.UniqueId,
                ExtraAttack = ruleAttackWithWeapon.ExtraAttack,
                IsFirstAttack = ruleAttackWithWeapon.IsFirstAttack,
                AttacksCount = ruleAttackWithWeapon.AttacksCount
            };

            return roll;
        }

        private InitiativeRoll CreateInitiativeRoll(RuleRollDice ruleRollDice, RuleInitiativeRoll ruleInitiativeRoll)
        {
            var roll = new InitiativeRoll
            {
                InitiatorId = ruleRollDice.Initiator?.UniqueId,
                Result = ruleRollDice.Result,
                ResultOverride = ruleRollDice.ResultOverride,
                RollHistory = [.. ruleRollDice.RollHistory ?? []],
                RuleRollName = ruleRollDice.Reason.Name,
                RuleRollType = ruleRollDice.Reason.Rule.GetType().Name,
                DiceType = ruleRollDice.DiceFormula.Dice,
                TotalModifiersBonus = ruleInitiativeRoll.Modifier
            };

            return roll;
        }

        private IMultiplayerParticipant GetMultiplayerParticipant()
        {
            return _multiplayerHost.IsActive ? _multiplayerHost : _multiplayerClient;
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

        private NetworkGame GetNetworkGame()
        {
            return _multiplayerHost.IsActive ? _multiplayerHost.CurrentGame : _multiplayerClient.CurrentGame;
        }

        private void OnLobbyCharacterOwnerChanged(int characterIndex, int playerIndex)
        {
            _logger.LogInformation("OnLobbyCharacterOwnerChanged. CharacterIndex={charIndex}, PlayerIndex={playerIndex}", characterIndex, playerIndex);
            _multiplayerHost.ChangeCharacterOwner(characterIndex, playerIndex);
        }
    }
}
