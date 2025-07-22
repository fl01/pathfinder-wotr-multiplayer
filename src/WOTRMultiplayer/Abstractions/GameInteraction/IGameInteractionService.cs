using System.Collections.Generic;
using System.Threading.Tasks;
using Kingmaker.UI;
using WOTRMultiplayer.MP.Entities;

namespace WOTRMultiplayer.Abstractions.GameInteraction
{
    public interface IGameInteractionService
    {
        bool IsPaused { get; }

        void LeaveArea(string areaExitId);

        void MarkSuggestedDialogAnswers(List<NetworkDialogAnswerSuggestion> suggestions);

        void MoveNonCombatCharacter(string unitId, NetworkVector3 destination, float delay, float orientation);

        void Pause(bool isPaused);

        void SelectDialogAnswer(string dialogName, string cueName, string answerName, string manualUnitSelectionId);

        void SetDialogContinueButtonState(bool isEnabled);

        void PlaySound(UISoundType type);

        Task<bool> StartDialogAsync(string dialogName, string targetUnitId, string initiatorUnitId, string mapObjectId, string speakerKey);

        List<NetworkCharacterOwnership> GetPartyPlayers();

        void ShowModalMessage(string error);

        bool IsUnitAI(string unitId);

        List<NetworkUnit> GetUnitsInCombat();

        void QuickLoadGame(string savePath);

        void LoadGameFromMainMenu(string savePath);

        string GetSaveGamePath();

        string GetPetOwnerId(string unitId);

        void StartTurnBasedCombatTurn(bool isActingInSurpriseRound);

        void EndTurnBasedCombatTurn();

        Task UpdateUnitsPositionAsync(List<NetworkUnit> networkUnits);

        void ClickUnitInCombat(NetworkClick click);

        void ClickGroundInCombat(NetworkClick click);

        void ClickAbilityInCombat(NetworkClick click);

        bool CombatTurnHasBeenFinished();
    }
}
