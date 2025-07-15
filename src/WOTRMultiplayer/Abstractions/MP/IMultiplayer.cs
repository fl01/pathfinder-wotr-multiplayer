using Kingmaker.Controllers.Clicks.Handlers;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Persistence;
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

        bool CanControlCharacter(bool original, string unitId);
        bool StartGameMode(GameModeType type);
        bool StopGameMode(GameModeType type);
        bool CanLeaveArea();
        bool OnBeforeRuleRollDiceTrigger(RuleRollDice ruleRollDice);
        void OnAfterRuleRollDiceTrigger(RuleRollDice ruleRollDice);

        void OnAfterCueShow(string dialogName, string cueName, bool hasSystemAnswer);
        bool OnBeforeSelectDialogAnswer(string dialogName, string cueName, string answerName, bool isExitAnswer, string manualUnitSelectionId);
        void OnAfterPlayDialogCue();
        bool StartDialog(string dialogName, string targetUnitId, string initiatorUnitId, string mapObjectId, string speakerKey);

        bool CanTickUnitCombatPrepareController();
        bool CanTickCombatController();

        bool OnBeforeStartTurn(string unitId, bool actingInSurpriseRound);

        bool OnBeforeEndTurn(string unitId);
        void ForceLoadGame(SaveInfo saveInfo);

        bool IsActive { get; }
    }
}
