using System.Collections.Generic;
using System.Threading.Tasks;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Entities.Dialogs;

namespace WOTRMultiplayer.Playground.Core.Dummies
{
    public class DummyDialogInteractionService : IDialogInteractionService
    {
        public void CloseDialogPopup(NetworkDialogPopup networkDialogPopup)
        {
        }

        public void MarkSuggestedDialogAnswers(List<NetworkDialogAnswerSuggestion> networkDialogAnswerSuggestions)
        {
        }

        public void PlayUnableToSelectCueAnimation(string answerName)
        {
        }

        public void ResetSuggestedDialogAnswers()
        {
        }

        public void SelectDialogAnswer(string answerName, string manualUnitSelectionId)
        {
        }

        public void SetDialogContinueButtonState(bool isEnabled)
        {
        }

        public Task<bool> StartDialogAsync(string dialogName, string targetUnitId, string initiatorUnitId, string mapObjectId, string speakerKey)
        {
            return Task.FromResult(false);
        }

        public void UpdateDialogPopupUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }
    }
}
