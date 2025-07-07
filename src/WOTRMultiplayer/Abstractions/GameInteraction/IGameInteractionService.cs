using System.Collections.Generic;
using System.Numerics;
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

        void MoveCharacter(string characterName, Vector3 destination, float delay, float orientation);

        void Pause(bool isPaused);

        void SelectDialogAnswer(string dialogName, string cueName, string answerName, string manualUnitSelectionId);

        void SetDialogContinueButtonState(bool isEnabled);

        void PlaySound(UISoundType type);

        Task<bool> StartDialogAsync(string dialogName, string targetUnitId, string initiatorUnitId, string mapObjectId, string speakerKey);

        List<NetworkCharacter> GetPartyPlayers();

        void ShowModalMessage(string error);
    }
}
