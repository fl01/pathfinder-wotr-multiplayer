using System.Collections.Generic;
using System.Numerics;
using WOTRMultiplayer.MP.Entities;

namespace WOTRMultiplayer.Abstractions.GameInteraction
{
    public interface IGameInteractionService
    {
        bool IsPaused { get; }

        void LeaveArea(string areaExitId);
        void MarkSuggestedDialogAnswers(List<NetworkDialogAnswerSuggestion> suggestions);
        void MoveCharacter(string characterName, Vector3 destination, float delay, float orientation);

        void Pause(bool isPaused);
        void SelectDialogAnswer(string dialogName, string cueName, string answerName, string manualUnitSelectionId);
        void SetDialogContinueButtonState(bool isEnabled);
    }
}
