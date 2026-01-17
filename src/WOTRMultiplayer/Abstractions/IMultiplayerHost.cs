using System.Collections.Generic;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.Area;
using WOTRMultiplayer.Entities.Dialogs;
using WOTRMultiplayer.Entities.GlobalMap;
using WOTRMultiplayer.Entities.Inspect;
using WOTRMultiplayer.Entities.Rest;

namespace WOTRMultiplayer.Abstractions
{
    public interface IMultiplayerHost : IMultiplayerActor
    {
        void Create(string gameId, NetworkGameStartUp gameStartUp);

        void ChangeHostedStartingPoint(string gameId, NetworkGameStartUp gameStartUp);

        bool Start();

        void ChangeCharacterOwner(NetworkCharacter character, NetworkPlayer player);

        void OnAreaTransition(NetworkAreaTransition areaTransition);

        void SendSelectedAnswer();

        void OnPerceptionCheck(NetworkPerceptionCheck check);

        void OnInspectionKnowledgeCheck(NetworkInspectionKnowledgeCheck check);

        void OnStealthPerceptionCheckRolled(NetworkStealthPerceptionCheck check);

        void OnCampingUseHealingSpellsChanged(bool isOn);

        void OnCampingStateChanged(NetworkCampingState state);

        void OnCampingUnitsRoleChanged(List<NetworkCampingRole> roles);

        void OnAfterTryRollRestRandomEncounter();

        void OnMakeVendorDeal();

        void OnCloseVendorWindow();

        void OnAcceptGroupChangerParty();

        void OnCloseGroupChangerPartyUI();

        void OnGlobalMapRestMenuOpened();

        void OnGlobalMapTravelStarted(NetworkGlobalMapTravel globalMapTravel);

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

        void OnGlobalMapSkipDay();

        void OnGlobalMapSelectedArmyChanged(string armyId);

        void OnGlobalMapAutoCrusadeCombatChanged(bool isEnabled);

        void OnZoneLootRemoveToggleChanged(bool removeUncollectedLoot);

        void OnDialogPopupClosed(NetworkDialogPopup networkDialogPopup);

        void OnCharacterSelectionWindowAccepted();

        void OnCharacterSelectionWindowClosed();

        void OnCharacterSelectionToggleChanged(string unitId);

        void OnNewGameDifficultyChanged(string difficulty);

        void OnCrusadeArmyCombatInitialized();
    }
}
