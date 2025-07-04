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
        private readonly IRollStorage _rollStorage;
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
            IRollStorage rollStorage)
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
            _logger.LogInformation("Creating Esc menu lobby item");
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
                    var roll = CreatePartyStatCheckRoll(ruleRollDice, rulePartyStatCheck);
                    _rollStorage.Add(roll);
                    break;
                case RuleRollD20:
                default:
                    _logger.LogWarning("Roll saving has been skipped. Type={rollType}", ruleRollDice.Reason.Rule.GetType().Name);
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
                    var roll = CreatePartyStatCheckRoll(ruleRollDice, rulePartyStatCheck);
                    var uniqueId = _rollStorage.GetUniqueId(roll);
                    var hostRoll = _multiplayerClient.GetRoll(uniqueId);
                    if (hostRoll == null)
                    {
                        _logger.LogError("Roll is missing => it will be rolled by the game");
                        return true;
                    }
                    ruleRollDice.m_Result = hostRoll.Result;
                    ruleRollDice.RollHistory.AddRange(hostRoll.RollHistory);
                    _logger.LogInformation("Roll results has been acquired from host. RollId={rollId}, Result={result}", uniqueId, ruleRollDice.Result);
                    return false;
                case RuleRollD20:
                default:
                    _logger.LogWarning("Roll retrieving has been skipped. Type={rollType}", ruleRollDice.Reason.Rule.GetType().Name);
                    break;
            }

            return true;
        }

        private PartyStatCheckRoll CreatePartyStatCheckRoll(RuleRollDice ruleRollDice, RulePartyStatCheck rulePartySkillCheck)
        {
            var roll = new PartyStatCheckRoll
            {
                InitiatorId = ruleRollDice.Initiator?.UniqueId,
                DifficultyClass = rulePartySkillCheck.DifficultyClass,
                StatType = rulePartySkillCheck.StatType,
                Result = ruleRollDice.Result,
                ResultOverride = ruleRollDice.ResultOverride,
                RollHistory = [.. ruleRollDice.RollHistory ?? []],
                RuleRollName = ruleRollDice.Reason.Name,
                RuleRollType = ruleRollDice.Reason.Rule.GetType().Name,
                DiceType = ruleRollDice.DiceFormula.Dice
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
