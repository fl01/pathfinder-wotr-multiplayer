using System.Collections.Generic;
using Kingmaker.EntitySystem.Persistence;
using Kingmaker.GameModes;
using Kingmaker.Items.Slots;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.GameInteraction.Contexts;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.ActionBar;
using WOTRMultiplayer.MP.Entities.Combat;
using WOTRMultiplayer.MP.Entities.Dialogs;
using WOTRMultiplayer.MP.Entities.Equipment;
using WOTRMultiplayer.MP.Entities.GlobalMap;
using WOTRMultiplayer.MP.Entities.Inspect;
using WOTRMultiplayer.MP.Entities.Leveling;
using WOTRMultiplayer.MP.Entities.Loot;
using WOTRMultiplayer.MP.Entities.MapObjects;
using WOTRMultiplayer.MP.Entities.Movement;
using WOTRMultiplayer.MP.Entities.Rest;
using WOTRMultiplayer.MP.Entities.Spells;
using WOTRMultiplayer.MP.Entities.Vendor;

namespace WOTRMultiplayer.Abstractions.MP
{
    public interface IMultiplayer
    {
        RemoteExecutionContext RemoteContext { get; }

        IUIFactory Factory { get; }

        IValueGenerator ValueGenerator { get; }

        bool InitializeMultiplayer(InitializeMultiplayerContext context);

        void TerminateMultiplayer();

        void InitializeEscMenuLobbyWindow(InitializeEscMenuLobbyWindowContext context);

        void MoveNonCombatCharacter(NetworkCharacterMove networkCharacterMove);

        bool OnStartGameMode(GameModeType type);

        bool OnStopGameMode(GameModeType type);

        bool CanLeaveArea();

        void OnAfterCueShow(string dialogName, string cueName, bool hasSystemAnswer);

        bool OnBeforeSelectDialogAnswer(string dialogName, string cueName, string answerName, bool isExitAnswer, string manualUnitSelectionId);

        void OnAfterPlayDialogCue();

        bool StartDialog(string dialogName, string targetUnitId, string initiatorUnitId, string mapObjectId, string speakerKey);

        bool CanTickUnitCombatPrepareController();

        bool CanTickCombatController();

        bool OnBeforeStartTurn(string unitId, bool actingInSurpriseRound);

        bool OnBeforeEndTurn(string unitId);

        void ForceLoadGame(SaveInfo saveInfo);

        bool IsControlledByPlayers(string unitId);

        bool IsControlledByLocalPlayer(string unitId);

        string GetMultiplayerOwnerName(string unitId);

        void OnClickUnit(NetworkClick networkClick);

        void OnClickGround(NetworkClick networkClick);

        void OnClickMapObject(NetworkClick networkClick);

        void OnInteractWithMapObjectOvertip(NetworkOvertip networkOvertip);

        void OnAbilityUse(NetworkAbility networkAbility);

        void OnToggleActivatableAbility(NetworkActivatableAbility networkActivatableAbility);

        bool IsActive { get; }

        bool IsInCombat { get; }

        void OnTransferInventoryItems(NetworkItemsTransfer networkItemsTransfer);

        void OnSkinLootContainer(NetworkLootableEntity networkLootableEntity);

        void OnDropItem(NetworkDropItem networkDropItem);

        bool CanUnitJoinCombat(string unitId);

        bool CanMakePerceptionCheck(string unitId, string mapObjectId);

        bool CanMakeStealthPerceptionCheck();

        bool OnInspectionKnowledgeCheck(NetworkInspectionKnowledgeCheck networkInspectionKnowledgeCheck);

        void OnPerceptionCheck(NetworkPerceptionCheck networkPerceptionCheck);

        void OnStealthPerceptionCheckRolled(NetworkStealthPerceptionCheck networkStealthPerceptionCheck);

        bool OnSpawnCampPlace(NetworkVector3 position);

        bool OnCampingUseHealingSpellsChanged(bool isActive);

        void OnCampingUnitsRoleChanged(List<NetworkCampingRole> networkCampingRoles);

        void OnStartRest();

        bool CanUseCampingUI();

        void OnBeforeTryRollRandomEncounter();

        void OnAfterTryRollRandomEncounter();

        int? GetNextRestBanter(int minInclusive, int maxExclusive);

        void OnInterrupRestBanterBark(NetworkRestBanter networkRestBanter);

        NetworkAIAction OnAfterAISelectedAction(NetworkAIAction networkAIAction);

        bool ShouldGroundHandlerMoveAllUnitsToPoint();

        void ResetExecutionContext();

        void OnTransferVendorItem(NetworkVendorItemTransfer networkVendorItemTransfer);

        bool CanFullyControlVendorUI();

        void OnMakeVendorDeal();

        void OnCloseVendorWindow();

        void OnMemorizeSpell(NetworkSpellSlot networkSpellSlot);

        void OnForgetSpell(NetworkSpellSlot networkSpellSlot);

        bool RequestLevelingUI(string unitId, NetworkLevelingType levelingType);

        void ForceLevelingUI(string unitId, NetworkLevelingType levelingType);

        void OnLevelingClassArchetypeSelected(string archetypeId);

        void OnLevelingClassSelected(string classId);

        void OnLevelingMythicClassSelected(string mythicClassId);

        void OnLevelingTerminated();

        bool CanMakeLevelingDecisions();

        void OnWitnessLevelingPhase(NetworkLevelingPhase networkLevelingPhase);

        void OnLevelingIncreaseSkillPoint(NetworkLevelingSkillPoint networkLevelingSkillPoint);

        void OnLevelingDecreaseSkillPoint(NetworkLevelingSkillPoint networkLevelingSkillPoint);

        void OnLevelingFeatureSelected(NetworkLevelingFeature networkLevelingFeature);

        void OnLevelingSpellRemoved(NetworkLevelingSpell networkLevelingSpell);

        void OnLevelingSpellChosen(NetworkLevelingSpell networkLevelingSpell);

        void OnLevelingCompleted();

        void OnLevelingIncreaseAbilityScore(NetworkLevelingAbilityScore networkLevelingAbilityScore);

        void OnLevelingDecreaseAbilityScore(NetworkLevelingAbilityScore networkLevelingAbilityScore);

        void OnLevelingPortraitSelected(NetworkLevelingPortrait portrait);

        void OnLevelingRaceSelected(string raceId);

        void OnLevelingGenderSelected(string genderId);

        void OnLevelingAlignmentSelected(string alignmentId);

        void OnLevelingNameChanged(string name);

        void OnLevelingRacialAbilityScoreBonusChanged(NetworkLevelingSequenceDirection direction);

        void OnLevelingBirthMonthChanged(NetworkLevelingSequenceDirection direction);

        void OnLevelingBirthDayChanged(NetworkLevelingSequenceDirection direction);

        void OnMoveActionBarSlot(NetworkActionBarSlot sourceActionBarSlot, NetworkActionBarSlot targetActionBarSlot);

        void OnClearActionBarSlot(NetworkActionBarSlot actionBarSlot);

        bool TogglePause(bool isPaused);

        void OnAutoPausedByTrapDetection();

        void OnLockpickInteraction(NetworkLockpickInteraction lockpickInteraction);

        void OnUnitAttackCommandStarted(NetworkUnitAttack networkUnitAttack);

        void OnHandleDelayCombatTurn(string unitId, string targetUnitId);

        void OnSetUnitStealthEnabled(string unitId, bool isEnabled, bool isForced);

        void OnShowGroupChangerUI();

        void OnCloseGroupChangerUI();

        bool OnClickGroupChangerUnit(string unitId);

        void OnAcceptGroupChangerParty();

        void OnGlobalMapRestOpened();

        bool OnGlobalMapBeforeRollTravelEncounter();

        void OnGlobalMapStartTravel(NetworkGlobalMapLocation destination);

        void OnSkipTimeOpened();

        void OnSkipTimeClosed();

        void OnSkipTimeHoursChanged(float hours);

        void OnSkipTimeStarted();

        bool OnGlobalMapSelectLocation(NetworkGlobalMapLocation globalMapLocation);

        void OnGlobalMapContinueTravel(NetworkGlobalMapState globalMapState);

        void OnGlobalMapStopTravel(NetworkGlobalMapState globalMapState);

        void OnGlobalMapMessageBoxShown();

        void OnGlobalMapMessageBoxClosed();

        void OnGlobalMapIngredientCollectionShown();

        void OnGlobalMapIngredientCollectionClosed();

        void OnGlobalMapIngredientCollectionAccepted(NetworkGlobalMapLocation globalMapLocation);

        void OnGlobalMapEnterLocation(NetworkGlobalMapLocation globalMapLocation);

        void OnGlobalMapEncounterMessageShown();

        void OnGlobalMapEncounterAccepted();

        void OnGlobalMapEncounterAvoided();

        void OnGlobalMapEncounterRolled(NetworkGlobalMapEncounter globalMapRandomEncounter);

        bool CanNavigateOnGlobalMap();

        int GetCombatSeed();

        void OnZoneLootShown();

        void OnZoneLootClosed();

        void OnZoneLootRemoveToggleChanged(bool removeUncollectedLoot);

        void OnZoneLootCompleted();

        void OnZoneLootCollectorButtonsUpdated();

        void OnDialogPopupShown(NetworkDialogPopup networkDialogPopup);

        void OnDialogPopupClosed(NetworkDialogPopup networkDialogPopup);

        void OnUseInventoryItem(NetworkUseInventoryItem useInventoryItem);

        NetworkEquipmentSlotPosition GetEquipmentSlotPosition(ItemSlot holdingSlot);
    }
}
