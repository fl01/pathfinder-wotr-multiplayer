using Kingmaker.EntitySystem.Persistence;
using Kingmaker.GameModes;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Damage;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Abilities;

namespace WOTRMultiplayer.Abstractions.MP
{
    public interface IMultiplayer
    {
        IUIFactory Factory { get; }

        bool InitializeMultiplayer(InitializeMultiplayerContext context);

        void TerminateMultiplayer();

        void InitializeEscMenuLobbyWindow(InitializeEscMenuLobbyWindowContext context);

        void MoveNonCombatCharacter(string unitId, NetworkVector3 destination, float delay, float orientation);

        bool StartGameMode(GameModeType type);

        bool StopGameMode(GameModeType type);

        bool CanLeaveArea();

        void OnAfterCueShow(string dialogName, string cueName, bool hasSystemAnswer);

        bool OnBeforeSelectDialogAnswer(string dialogName, string cueName, string answerName, bool isExitAnswer, string manualUnitSelectionId);

        void OnAfterPlayDialogCue();

        bool StartDialog(string dialogName, string targetUnitId, string initiatorUnitId, string mapObjectId, string speakerKey);

        bool CanTickUnitCombatPrepareController();

        bool CanTickCombatController();

        bool OnBeforeStartTurn(string unitId, bool actingInSurpriseRound);

        bool OnBeforeEndTurn(string unitId);

        void ForceLoadGame(SaveInfo saveInfo);

        bool IsControlledByPlayers(string unitId);

        bool IsControlledByLocalPlayer(string unitId);

        void OnClickUnit(NetworkClick click);

        void OnClickGround(NetworkClick click);

        void OnAbilityUse(NetworkAbility ability);

        void OnToggleActivatableAbility(NetworkActivatableAbility ability);

        bool IsActive { get; }

        NetworkActionsState GetActionsState();

        bool OnBeforeRuleRollDiceTrigger(RuleRollDice ruleRollDice);
        void OnAfterRuleRollDiceTrigger(RuleRollDice ruleRollDice);

        bool OnBeforeRuleCalculateDamageTrigger(RuleCalculateDamage ruleCalculateDamage);
        void OnAfterRuleCalculateDamageTrigger(RuleCalculateDamage ruleCalculateDamage);

        int OnAfterRollRuleHealDamage(RuleHealDamage instance, int unitsCount, int result);

        bool OnBeforeRuleAttackRoll(RuleAttackRoll ruleAttackRoll);
        void OnAfterRuleAttackRollTrigger(RuleAttackRoll ruleAttackRoll);

        void OnAfterRuleSavingThrowTrigger(RuleSavingThrow ruleSavingThrow);
        void OnBeforeRuleSavingThrowRoll(RuleSavingThrow ruleSavingThrow);

        bool OnBeforeRuleSpellResistanceCheckRoll(RuleSpellResistanceCheck ruleSpellResistanceCheck);
        void OnAfterRuleSpellResistanceCheckTrigger(RuleSpellResistanceCheck ruleSpellResistanceCheck);
    }
}
