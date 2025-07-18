using System.Linq;
using System.Numerics;
using Kingmaker.Controllers.Clicks.Handlers;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Persistence;
using Kingmaker.GameModes;
using Kingmaker.RuleSystem.Rules;
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

        public bool CanControlCharacter(bool original, string unitId)
        {
            // no need to possibly override value if this character is not controllable at all
            if (!original)
            {
                {
                    return original;
                }
            }

            var multiplayerParticipant = GetMultiplayerParticipant();
            if (multiplayerParticipant == null)
            {
                return false;
            }

            return multiplayerParticipant.CanControlCharacter(unitId);
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
        /// Combat: host+client - store roll if you are in control of character
        /// Non-Combat: host - store all rolls, client - ignore
        /// </summary>
        /// <param name="ruleRollDice"></param>
        public void OnAfterRuleRollDiceTrigger(RuleRollDice ruleRollDice)
        {
            var multiplayerParticipant = GetMultiplayerParticipant();
            if (multiplayerParticipant == null || !multiplayerParticipant.ShouldStoreRoll())
            {
                return;
            }

            var combatRound = multiplayerParticipant.GetCombatRound();
            NetworkDiceRoll roll = CreateNetworkDiceRoll(ruleRollDice, combatRound);

            var rollType = ruleRollDice.Reason.Rule.GetType().Name;
            if (roll == null)
            {
                _logger.LogWarning("Roll saving has been skipped. Type={rollType}, InitiatorName={initiatorName}, InitiatorId={initiatorId}", rollType, ruleRollDice.Initiator?.CharacterName, ruleRollDice.Initiator?.UniqueId);
                return;
            }

            if (!_diceRollStorage.Save(roll))
            {
                var message = $"Roll has not been saved which guarantees to cause desync in the game. RollType={rollType}";
                _logger.LogCritical(message);
                _gameInteractionService.ShowModalMessage(message);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="ruleRollDice"></param>
        /// <returns>true-roll should be performed by game</returns>
        public bool OnBeforeRuleRollDiceTrigger(RuleRollDice ruleRollDice)
        {
            var multiplayerParticipant = GetMultiplayerParticipant();
            if (multiplayerParticipant == null || multiplayerParticipant.ShouldStoreRoll())
            {
                return true;
            }

            var initiatorId = ruleRollDice.Initiator.UniqueId;
            var combatRound = multiplayerParticipant.GetCombatRound();
            var rollType = ruleRollDice.Reason.Rule.GetType().Name;

            var networkDiceRoll = CreateNetworkDiceRoll(ruleRollDice, combatRound);
            if (networkDiceRoll == null)
            {
                _logger.LogWarning("Roll retrieving has been skipped. Type={rollType}, InitiatorName={initiatorName}, InitiatorId={initiatorId}", rollType, ruleRollDice.Initiator?.CharacterName, ruleRollDice.Initiator?.UniqueId);
                return true;
            }

            var networkDiceRollId = _diceRollStorage.GetUniqueId(networkDiceRoll);

            var roll = multiplayerParticipant.RetrieveRoll(networkDiceRollId, initiatorId);
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

        public bool OnBeforeStartTurn(string unitId, bool actingInSurpriseRound)
        {
            var participant = GetMultiplayerParticipant();
            if (participant == null)
            {
                return true;
            }

            return participant.OnBeforeStartTurn(unitId, actingInSurpriseRound);
        }

        public bool OnBeforeEndTurn(string unitId)
        {
            var participant = GetMultiplayerParticipant();
            return participant.OnBeforeEndTurn(unitId);
        }

        public void ForceLoadGame(SaveInfo saveInfo)
        {
            // extra validation is not required since everything is already validated by the game
            var savePath = saveInfo.FolderName;
            _logger.LogInformation("Force load game. Save={saveLocation}", savePath);
            var participant = GetMultiplayerParticipant();
            participant.ForceLoadGame(savePath);
        }

        public bool CanBeControlledByAI(string unitId)
        {
            var participant = GetMultiplayerParticipant();
            if (participant == null || participant.CurrentGame?.Combat == null)
            {
                return true;
            }

            var realUnitId = _gameInteractionService.GetPetOwnerId(unitId) ?? unitId;
            var partyMember = participant.CurrentGame.Characters.FirstOrDefault(c => string.Equals(c.UnitId, realUnitId, System.StringComparison.OrdinalIgnoreCase));
            return partyMember == null;
        }

        public void OnClickUnit(NetworkClick click)
        {
            var multiplayerParticipant = GetMultiplayerParticipant();
            if (multiplayerParticipant == null)
            {
                return;
            }

            multiplayerParticipant.OnClickUnit(click);
        }

        private NetworkDiceRoll CreateNetworkDiceRoll(RuleRollDice ruleRollDice, int combatRound)
        {
            NetworkDiceRoll roll = ruleRollDice.Reason.Rule switch
            {
                RulePartyStatCheck rulePartyStatCheck => CreatePartyStatCheckRoll(ruleRollDice, rulePartyStatCheck),
                RuleInitiativeRoll ruleInitiativeRoll => CreateInitiativeRoll(ruleRollDice, ruleInitiativeRoll),
                RuleAttackWithWeapon ruleAttackWithWeapon => CreateAttackWithWeaponRoll(ruleRollDice, ruleAttackWithWeapon, combatRound),
                _ => null
            };

            return roll;
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
                AttacksCount = ruleAttackWithWeapon.AttacksCount,
                IsCriticalRoll = ruleAttackWithWeapon.AttackRoll.IsCriticalRoll
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
