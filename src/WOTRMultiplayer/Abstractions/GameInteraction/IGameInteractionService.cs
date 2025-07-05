using System.Numerics;

namespace WOTRMultiplayer.Abstractions.GameInteraction
{
    public interface IGameInteractionService
    {
        bool IsPaused { get; }

        void LeaveArea(string areaExitId);

        void MoveCharacter(string characterName, Vector3 destination, float delay, float orientation);

        void Pause(bool isPaused);

        void SetDialogContinueButtonState(bool isEnabled);
    }
}
