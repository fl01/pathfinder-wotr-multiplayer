using System.Collections.Generic;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.Area;
using WOTRMultiplayer.Entities.Combat.Crusades;
using WOTRMultiplayer.Entities.Dialogs;
using WOTRMultiplayer.Entities.GlobalMap;
using WOTRMultiplayer.Entities.Inspect;
using WOTRMultiplayer.Entities.Rest;
using WOTRMultiplayer.Services.GameInteraction.Contexts;

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

        void OnAfterTryRollRestRandomEncounter(NetworkRandomEncounterContext encounterContext);

        void OnMakeVendorDeal();

        void OnCloseVendorWindow();

        void OnAcceptGroupChangerParty();

        void OnCloseGroupChangerPartyUI();

        void OnGlobalMapLocationMessageClosed();

        void OnGlobalMapCommonPopupDeclined(NetworkGlobalMapCommonPopup globalMapCommonPopup);

        void OnGlobalMapRestMenuOpened();

        void OnGlobalMapTravelStarted(NetworkGlobalMapTravel globalMapTravel);

        void OnSkipTimeClosed();

        void OnSkipTimeHoursChanged(float hours);

        void OnSkipTimeStarted();

        void OnGlobalMapContinueTravel(NetworkGlobalMapTraveler globalMapTraveler);

        void OnGlobalMapStopTravel(NetworkGlobalMapTraveler globalMapTraveler);

        void OnGlobalMapCommonPopupAccepted(NetworkGlobalMapCommonPopup globalMapCommonPopup);

        void OnGlobalMapEnterLocation(NetworkGlobalMapLocation globalMapLocation);

        void OnGlobalMapEncounterAccepted();

        void OnGlobalMapEncounterAvoided();

        void OnGlobalMapRandomEncounterRolled(NetworkGlobalMapEncounter globalMapEncounter);

        void OnGlobalMapSkipDay();

        void OnGlobalMapSelectedArmyChanged(NetworkGlobalMapArmy globalMapArmy);

        void OnGlobalMapAutoCrusadeCombatChanged(bool isEnabled);

        void OnZoneLootRemoveToggleChanged(bool removeUncollectedLoot);

        void OnZoneLootCompleted();

        void OnDialogPopupClosed(NetworkDialogPopup networkDialogPopup);

        void OnCharacterSelectionWindowAccepted();

        void OnCharacterSelectionWindowClosed();

        void OnCharacterSelectionToggleChanged(string unitId);

        void OnNewGameDifficultyChanged(string difficulty);

        void OnTacticalCombatInitialized();

        void OnCrusadeArmyBattleResultsClosed();

        void OnCrusadeArmyBattleResultsManualCombatStarted();

        void OnGlobalMapCombatResultsClosed();

        void OnTacticalCombatUnitUseAbilityCommand(NetworkTacticalUnitUseAbilityCommand tacticalUnitUseAbilityCommand);

        void OnTacticalCombatUnitAttackCommand(NetworkTacticalUnitAttackCommand tacticalUnitAttackCommand);

        void OnTacticalCombatUnitMoveToCommand(NetworkTacticalUnitMoveToCommand tacticalUnitMoveToCommand);

        bool OnTacticalCombatTotalDefenseUsed();

        bool OnTacticalCombatTurnPostponed();

        void OnTacticalCombatRetreat();

        bool OnGlobalMapCrusadeArmySquadSplitted(NetworkGlobalMapArmySquadSlot globalMapArmySquadSlot, int count);

        void OnGlobalMapCrusadeArmySquadsMerged(NetworkGlobalMapArmySquadSlot globalMapArmySquadSlot, NetworkGlobalMapArmySquadSlot targetSquadSlot, int count);

        void OnGlobalMapCrusadeArmySquadsSwitched(NetworkGlobalMapArmySquadSlot globalMapArmySquadSlot, NetworkGlobalMapArmySquadSlot targetSquadSlot);

        void OnGlobalMapCrusadeArmySquadSplitRequested(NetworkGlobalMapArmySquadSlot globalMapArmySquadSlot, NetworkGlobalMapArmySquadSlot targetSquadSlot, int count);

        bool OnGlobalMapCrusadeArmyMergedInOne(NetworkGlobalMapArmySquadSlot globalMapArmySquadSlot);

        void OnGlobalMapCrusadeArmySquadDismiss(NetworkGlobalMapArmySquadSlot globalMapArmySquadSlot);

        void OnGlobalMapCrusadeArmyDismiss(NetworkGlobalMapArmy globalMapArmy);

        void OnGlobalMapCrusadeArmyInfoClosed();

        void OnGlobalMapCrusadeArmyMoveSquadsToMainArmy();

        void OnGlobalMapCrusadeArmyMoveSquadsToSecondArmy();

        void OnGlobalMapCrusadeArmyInfoNextMergeArmy();

        void OnGlobalMapCrusadeArmyInfoPrevMergeArmy();

        void OnGlobalMapCrusadeArmyLeaderAction(NetworkGlobalMapArmyLeader globalMapArmyLeader, NetworkGlobalMapArmyLeaderActionType armyLeaderActionType);

        void OnGlobalMapMergeArmies();

        void OnGlobalMapCrusadeArmyInfoCreateArmy();

        void OnGlobalMapCrusadeArmyInfoMainClosed();

        void OnGlobalMapCrusadeArmyInfoMainNameChanged(NetworkGlobalMapArmy globalMapArmy);

        void OnGlobalMapCrusadeArmyInfoMergeNameChanged(NetworkGlobalMapArmy globalMapArmy);

        void OnGlobalMapCrusadeArmySetLeaderClear();

        void OnGlobalMapCrusadeArmySetLeaderRecruit();
    }
}
