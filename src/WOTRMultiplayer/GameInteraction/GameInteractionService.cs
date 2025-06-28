using System.Linq;
using System.Numerics;
using Kingmaker;
using Kingmaker.GameModes;
using Kingmaker.UnitLogic.Commands;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.GameInteraction;

namespace WOTRMultiplayer.GameInteraction
{
    public class GameInteractionService : IGameInteractionService
    {
        private readonly ILogger<GameInteractionService> _logger;

        public GameInteractionService(ILogger<GameInteractionService> logger)
        {
            _logger = logger;
        }

        public void MoveCharacter(string characterName, Vector3 destination, float delay, float orientation)
        {
            var character = Game.Instance.Player.PartyAndPets.FirstOrDefault(f => string.Equals(f.CharacterName, characterName));
            if (character == null)
            {
                _logger.LogError("Can't find character. Name={characterName}", characterName);
                return;
            }

            var unityDestination = new UnityEngine.Vector3(destination.X, destination.Y, destination.Z);
            var command = new UnitMoveTo(unityDestination, 0.3f)
            {
                MovementDelay = delay,
                Orientation = orientation,
                CreatedByPlayer = true
            };
            character.Commands.Run(command);
        }

        public void Pause(bool isPaused)
        {
            _logger.LogInformation("Pause game. Value={isPaused}", isPaused);
            if (isPaused)
            {
                Game.Instance.StartMode(GameModeType.Pause);
                return;
            }

            Game.Instance.StopMode(GameModeType.Pause);
        }
    }
}
