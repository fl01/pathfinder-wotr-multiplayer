using System.Collections.Generic;
using System.Threading.Tasks;
using WOTRMultiplayer.Entities.Dialogs;

namespace WOTRMultiplayer.Abstractions.GameInteraction
{
    public interface IDialogInteractionService
    {
        void MarkSuggestedDialogAnswers(List<NetworkDialogAnswerSuggestion> networkDialogAnswerSuggestions);

        void ResetSuggestedDialogAnswers();

        void SelectDialogAnswer(string answerName, string manualUnitSelectionId);

        void SetDialogContinueButtonState(bool isEnabled);

        Task<bool> StartDialogAsync(string dialogName, string targetUnitId, string initiatorUnitId, string mapObjectId, string speakerKey);

        void UpdateDialogPopupUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void CloseDialogPopup(NetworkDialogPopup networkDialogPopup);

        void PlayUnableToSelectCueAnimation(string answerName);
    }
}
