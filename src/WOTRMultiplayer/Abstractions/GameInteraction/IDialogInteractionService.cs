using System.Collections.Generic;
using System.Threading.Tasks;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.Dialogs;

namespace WOTRMultiplayer.Abstractions.GameInteraction
{
    public interface IDialogInteractionService
    {
        void MarkSuggestedCueAnswers(List<NetworkPlayer> allPlayers, List<NetworkDialogAnswerSuggestion> networkDialogAnswerSuggestions);

        void ResetSuggestedDialogAnswers();

        void SelectDialogAnswer(string answerName, string manualUnitSelectionId);

        void SetDialogContinueButtonState(bool isEnabled);

        Task<bool> StartDialogAsync(NetworkDialog networkDialog);

        void UpdateDialogPopupUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void CloseDialogPopup(NetworkDialogPopup networkDialogPopup);

        void AcceptDialogPopup(NetworkDialogPopup networkDialogPopup);

        void PlayUnableToSelectCueAnimation(string answerName);
    }
}
