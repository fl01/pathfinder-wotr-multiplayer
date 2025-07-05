using Kingmaker.Controllers.Clicks.Handlers;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.GameModes;
using Kingmaker.RuleSystem.Rules;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.MP.Entities;

namespace WOTRMultiplayer.Abstractions.MP
{
    public interface IMultiplayer
    {
        IUIFactory Factory { get; }

        bool InitializeMultiplayer(InitializeMultiplayerContext context);

        void TerminateMultiplayer();

        void InitializeEscMenuLobbyWindow(InitializeEscMenuLobbyWindowContext context);

        void MoveCharacter(UnitEntityData unit, ClickGroundHandler.CommandSettings settings);

        bool CanControlCharacter(string characterName);
        bool StartGameMode(GameModeType type);
        bool StopGameMode(GameModeType type);
        bool CanLeaveArea();
        bool OnBeforeRuleRollDiceTrigger(RuleRollDice ruleRollDice);
        void OnAfterRuleRollDiceTrigger(RuleRollDice ruleRollDice);

        void OnAfterCueShow(string dialogName, string cueName, bool hasSystemAnswer);

        bool IsActive { get; }
    }
}
