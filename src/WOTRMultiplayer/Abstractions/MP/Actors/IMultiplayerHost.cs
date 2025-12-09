using System.Collections.Generic;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.GlobalMap;
using WOTRMultiplayer.MP.Entities.Inspect;
using WOTRMultiplayer.MP.Entities.Rest;

namespace WOTRMultiplayer.Abstractions.MP.Actors
{
    public interface IMultiplayerHost : IMultiplayerActor
    {
        void Create(string saveFilePath, string gameId, List<NetworkCharacter> characters);

        void UpdateSaveGame(string saveFilePath, string gameId, List<NetworkCharacter> characters);

        void Start();

        void ChangeCharacterOwner(int characterIndex, int playerIndex);

        void LeaveArea(string areaExitId);

        void SendSelectedAnswer();

        void OnPerceptionCheck(NetworkPerceptionCheck check);
        void OnInspectionKnowledgeCheck(NetworkInspectionKnowledgeCheck check);

        void OnCampingUseHealingSpellsChanged(bool isOn);

        void OnCampingStateChanged(NetworkCampingState state);

        void OnCampingUnitsRoleChanged(List<NetworkCampingRole> roles);

        void OnStartRest();

        void OnAfterTryRollRandomEncounter();

        void OnMakeVendorDeal();

        void OnCloseVendorWindow();

        void OnAcceptGroupChangerParty();

        void OnCloseGroupChangerPartyUI();

        void OnGlobalMapRestMenuOpened();

        void OnGlobalMapStartTravel(NetworkGlobalMapLocation destination);

        void OnSkipTimeClosed();

        void OnSkipTimeHoursChanged(float hours);

        void OnSkipTimeStarted();

        void OnGlobalMapContinueTravel(NetworkGlobalMapState globalMapState);

        void OnGlobalMapStopTravel(NetworkGlobalMapState globalMapState);

        void OnGlobalMapIngredientCollectionAccepted(NetworkGlobalMapLocation globalMapLocation);

        void OnGlobalMapEnterLocation(NetworkGlobalMapLocation globalMapLocation);

        void OnGlobalMapEncounterAccepted();

        void OnGlobalMapEncounterAvoided();

        void OnGlobalMapRandomEncounterRolled(NetworkGlobalMapEncounter globalMapEncounter);

        void OnZoneLootRemoveToggleChanged(bool removeUncollectedLoot);
    }
}
