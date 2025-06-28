using System.Numerics;

namespace WOTRMultiplayer.Abstractions.GameInteraction
{
    public interface IGameInteractionService
    {
        void MoveCharacter(string characterName, Vector3 destination, float delay, float orientation);

        void Pause(bool isPaused);
    }
}
