using System.Collections.Generic;
using Kingmaker.GameModes;
using Kingmaker.Items.Slots;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.ActionBar;
using WOTRMultiplayer.Entities.Area;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.Combat.Crusades;
using WOTRMultiplayer.Entities.Dialogs;
using WOTRMultiplayer.Entities.Equipment;
using WOTRMultiplayer.Entities.GlobalMap;
using WOTRMultiplayer.Entities.Inspect;
using WOTRMultiplayer.Entities.Items;
using WOTRMultiplayer.Entities.Leveling;
using WOTRMultiplayer.Entities.MapObjects;
using WOTRMultiplayer.Entities.Movement;
using WOTRMultiplayer.Entities.NewGame;
using WOTRMultiplayer.Entities.Rest;
using WOTRMultiplayer.Entities.Spells;
using WOTRMultiplayer.Entities.Vendor;
using WOTRMultiplayer.Services.GameInteraction.Contexts;

namespace WOTRMultiplayer.Abstractions
{
    public interface IMultiplayer
    {
        RemoteExecutionContext RemoteContext { get; }

        IUIFactory Factory { get; }

        IValueGenerator ValueGenerator { get; }

        bool InitializeMultiplayer(InitializeMultiplayerContext context);

        void TerminateMultiplayer();

        void InitializeEscMenuLobbyWindow();

        void MoveNonCombatCharacter(NetworkCharacterMove networkCharacterMove);

        void OnStartGameMode(GameModeType type);

        void OnStopGameMode(GameModeType type);

        bool CanInitiateAreaTransitions();

        void OnAfterCueShow(string dialogName, string cueName, bool hasSystemAnswer);

        bool OnBeforeSelectDialogAnswer(string dialogName, string cueName, string answerName, bool isExitAnswer, string manualUnitSelectionId);

        void OnAfterPlayDialogCue();

        bool StartDialog(string dialogName, string targetUnitId, string initiatorUnitId, string mapObjectId, string speakerKey);

        bool CanTickUnitCombatPrepareController();

        bool CanTickCombatController();

        bool OnBeforeStartTurn(string unitId, bool actingInSurpriseRound);

        bool OnBeforeEndTurn(string unitId);

        void ForceLoadGame(string gameId, string savePath);

        bool IsControlledByPlayers(string unitId);

        bool IsControlledByLocalPlayer(string unitId);

        string GetCharacterOwnerName(string unitId);

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

        bool CanMakeInspectionKnowledgeCheck();

        void OnInspectionKnowledgeCheck(NetworkInspectionKnowledgeCheck networkInspectionKnowledgeCheck);

        void OnPerceptionCheck(NetworkPerceptionCheck networkPerceptionCheck);

        void OnStealthPerceptionCheckRolled(NetworkStealthPerceptionCheck networkStealthPerceptionCheck);

        bool OnSpawnCampPlace(NetworkVector3 position);

        bool OnCampingUseHealingSpellsChanged(bool isActive);

        void OnCampingUnitsRoleChanged(List<NetworkCampingRole> networkCampingRoles);

        void OnStartRest();

        void OnStartRestSleepPhase();

        bool CanUseCampingUI();

        void OnBeforeTryRollRestRandomEncounter();

        void OnAfterTryRollRestRandomEncounter();

        int? GetNextRestBanter(int minInclusive, int maxExclusive);

        void OnInterrupRestBanterBark(NetworkRestBanter networkRestBanter);

        NetworkAIAction OnAfterAISelectedAction(NetworkAIAction networkAIAction);

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

        void OnLevelingPortraitSelected(NetworkLevelingPortrait levelingPortrait);

        void OnLevelingVoiceSelected(NetworkLevelingVoice levelingVoice);

        void OnLevelingRaceSelected(string raceId);

        void OnLevelingGenderSelected(string genderId);

        void OnLevelingAlignmentSelected(string alignmentId);

        void OnLevelingNameChanged(string name);

        void OnLevelingRacialAbilityScoreBonusChanged(NetworkLevelingSequenceDirection direction);

        void OnLevelingBirthMonthChanged(NetworkLevelingSequenceDirection direction);

        void OnLevelingBirthDayChanged(NetworkLevelingSequenceDirection direction);

        void OnLevelingRespecCompleted();

        void OnLevelingRespecWindowShown(string unitId);

        void OnLevelingRespecLevelUp();

        void OnLevelingRespecMythicLevelUp();

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

        void OnGlobalMapTravelStarted(NetworkGlobalMapTravel globalMapTravel);

        void OnSkipTimeOpened();

        void OnSkipTimeClosed();

        void OnSkipTimeHoursChanged(float hours);

        void OnSkipTimeStarted();

        bool OnGlobalMapSelectLocation(NetworkGlobalMapLocation globalMapLocation);

        void OnGlobalMapContinueTravel(NetworkGlobalMapTraveler globalMapTraveler);

        void OnGlobalMapStopTravel(NetworkGlobalMapTraveler globalMapTraveler);

        void OnGlobalMapMessageBoxShown();

        void OnGlobalMapLocationMessageClosed();

        void OnGlobalMapCommonPopupShown(NetworkGlobalMapCommonPopup globalMapCommonPopup);

        void OnGlobalMapCommonPopupDeclined(NetworkGlobalMapCommonPopup globalMapCommonPopup);

        void OnGlobalMapCommonPopupAccepted(NetworkGlobalMapCommonPopup globalMapCommonPopup);

        void OnGlobalMapEnterLocation(NetworkGlobalMapLocation globalMapLocation);

        void OnGlobalMapEncounterMessageShown();

        void OnGlobalMapEncounterAccepted();

        void OnGlobalMapEncounterAvoided();

        void OnGlobalMapEncounterRolled(NetworkGlobalMapEncounter globalMapRandomEncounter);

        bool CanNavigateOnGlobalMap();

        void OnGlobalMapSkipDay();

        void OnGlobalMapDisposed();

        void OnGlobalMapTravelerModeChanged(NetworkGlobalMapTravelerMode travelerMode);

        void OnGlobalMapSelectedArmyChanged(string armyId);

        void OnGlobalMapAutoCrusadeCombatChanged(bool isEnabled);

        int? GetCombatSeed();

        int? GetSessionSeed();

        void OnZoneLootShown();

        void OnZoneLootClosed();

        void OnZoneLootRemoveToggleChanged(bool removeUncollectedLoot);

        void OnZoneLootCompleted();

        void OnZoneLootCollectorButtonsUpdated();

        void OnDialogPopupShown(NetworkDialogPopup networkDialogPopup);

        void OnDialogPopupClosed(NetworkDialogPopup networkDialogPopup);

        void OnUseInventoryItem(NetworkUseInventoryItem useInventoryItem);

        NetworkEquipmentSlotPosition GetEquipmentSlotPosition(ItemSlot holdingSlot);

        void OnLevelingBodyTypeAppearanceChanged(int index);

        void OnLevelingFaceAppearanceChanged(int index);

        void OnLevelingScarAppearanceChanged(int index);

        void OnLevelingHairStyleAppearanceChanged(int index);

        void OnLevelingHornsAppearanceChanged(int index);

        void OnLevelingWarpaintAppearanceChanged(NetworkLevelingWarpaint warpaint);

        void OnLevelingTattooAppearanceChanged(NetworkLevelingTattoo tattoo);

        void OnLevelingBodyColorAppearanceChanged(string textureName);

        void OnLevelingEyesColorAppearanceChanged(string textureName);

        void OnLevelingHairColorAppearanceChanged(string textureName);

        void OnLevelingHornsColorAppearanceChanged(string textureName);

        void OnLevelingWarpaintColorAppearanceChanged(NetworkLevelingWarpaint warpaint);

        void OnLevelingTattooColorAppearanceChanged(NetworkLevelingTattoo tattoo);

        void OnLevelingPrimaryOutfitColorAppearanceChanged(string textureName);

        void OnLevelingSecondaryOutfitColorAppearanceChanged(string textureName);

        bool CanControlCharacterSelectionWindow();

        void OnCharacterSelectionWindowShown();

        void OnCharacterSelectionWindowAccepted();

        void OnCharacterSelectionWindowClosed();

        void OnCharacterSelectionToggleChanged(string unitId);

        void OnNewGameDifficultyChanged(string difficulty);

        bool CanMakeNewGameSequenceDecisions();

        string GetNewGameSequenceId();

        void OnNewGameSequenceWitnessPhase(NetworkNewGameSequencePhase phase);

        bool OnCreateAndEquipPolymorphInSlot(NetworkPolymorphicItem polymorphicItem);

        void OnCutsceneSkip();

        void OnAreaTransition(NetworkAreaTransition areaTransition);

        bool OnTacticalCombatInitialization();

        void OnTacticalCombatEnded();

        void OnTacticalCombatInitialized();

        bool OnBeforeTacticalCombatTurnStart(int turnNumber);

        void OnCrusadeArmyCombatTurnStarted(NetworkArmyCombatTurn armyCombatTurn);

        int? GetCrusadeArmyCombatSeed();

        void OnCrusadeArmyBattleResultsShown();

        void OnCrusadeArmyBattleResultsClosed();

        void OnCrusadeArmyBattleResultsManualCombatStarted();

        void OnGlobalMapCombatResultsShown();

        void OnGlobalMapCombatResultsClosed();

        void OnTacticalCombatUnitMoveToCommand(NetworkTacticalUnitMoveToCommand tacticalUnitMoveToCommand);

        void OnTacticalCombatUnitAttackCommand(NetworkTacticalUnitAttackCommand tacticalUnitAttackCommand);

        void OnTacticalCombatUnitUseAbilityCommand(NetworkTacticalUnitUseAbilityCommand tacticalUnitUseAbilityCommand);

        bool CanControlTacticalCombat();

        bool OnTacticalCombatTotalDefenseUsed();

        bool OnTacticalCombatTurnPostponed();

        void OnTacticalCombatRetreat();
    }
}
