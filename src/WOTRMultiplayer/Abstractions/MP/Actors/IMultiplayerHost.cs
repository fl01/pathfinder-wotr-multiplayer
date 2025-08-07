using System.Collections.Generic;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.MapObjects;

namespace WOTRMultiplayer.Abstractions.MP.Actors
{
    public interface IMultiplayerHost : IMultiplayerActor
    {
        void Create(string saveFilePath, string gameId, List<NetworkCharacterOwnership> characters);

        void UpdateSaveGame(string saveFilePath, string gameId, List<NetworkCharacterOwnership> characters);

        void Start();

        void ChangeCharacterOwner(int characterIndex, int playerIndex);

        void LeaveArea(string areaExitId);

        void SendSelectedAnswer();

        void OnPerceptionCheck(NetworkPerceptionCheck check);
    }
}
