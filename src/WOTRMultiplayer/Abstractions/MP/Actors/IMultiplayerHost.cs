using System.Collections.Generic;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Inspect;
using WOTRMultiplayer.MP.Entities.Rest;

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
        void OnInspectionKnowledgeCheck(NetworkInspectionKnowledgeCheck check);

        bool OnSpawnCampPlace(NetworkVector3 position);

        void OnCampingUseHealingSpellsChanged(bool isOn);

        void OnCampingStateChanged(NetworkCampingState state);

        void OnCampingUnitsRoleChanged(List<NetworkCampingRole> roles);

        void OnStartRest();

        void OnAfterTryRollRandomEncounter();

        void OnMakeVendorDeal();
        void OnCloseVendorWindow();
    }
}
