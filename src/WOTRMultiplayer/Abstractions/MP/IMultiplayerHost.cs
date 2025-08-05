using System.Collections.Generic;
using WOTRMultiplayer.MP.Entities;

namespace WOTRMultiplayer.Abstractions.MP
{
    public interface IMultiplayerHost : IMultiplayerActor
    {
        void Create(string saveFilePath, string gameId, List<NetworkCharacterOwnership> characters);

        void UpdateSaveGame(string saveFilePath, string gameId, List<NetworkCharacterOwnership> characters);

        void Start();

        void ChangeCharacterOwner(int characterIndex, int playerIndex);

        void LeaveArea(string areaExitId);

        void SendSelectedAnswer();
    }
}
