using System;
using System.Collections.Generic;
using Kingmaker.Controllers.Rest;
using Kingmaker.GameModes;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.ActionBar;
using WOTRMultiplayer.Entities.Area;
using WOTRMultiplayer.Entities.AreaEffects;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.Combat.Crusades;
using WOTRMultiplayer.Entities.Dialogs;
using WOTRMultiplayer.Entities.Equipment;
using WOTRMultiplayer.Entities.GlobalMap;
using WOTRMultiplayer.Entities.Items;
using WOTRMultiplayer.Entities.Leveling;
using WOTRMultiplayer.Entities.MapObjects;
using WOTRMultiplayer.Entities.Movement;
using WOTRMultiplayer.Entities.NewGame;
using WOTRMultiplayer.Entities.Ping;
using WOTRMultiplayer.Entities.Rest;
using WOTRMultiplayer.Entities.Rolls.Claiming.Values;
using WOTRMultiplayer.Entities.Spells;
using WOTRMultiplayer.Entities.Vendor;

namespace WOTRMultiplayer.Abstractions
{
    public interface IMultiplayerActor
    {
        NetworkGameConnectivity GetGameConnectivity();

        List<NetworkPlayer> GetPlayers();

        List<NetworkPlayer> GetOtherPlayers();

        List<NetworkCharacter> GetCharacters();

        bool ReadyChanged();

        void MoveNonCombatCharacter(NetworkCharacterMove move);

        NetworkArea CurrentArea { get; }

        bool IsInCombat { get; }

        bool IsActive { get; }

        bool IsInLobby { get; }

        void Reset();

        Action<NetworkGameConnectivity> OnConnected { get; set; }

        Action<NetworkLobbyStage, List<NetworkPlayer>> OnPlayersChanged { get; set; }

        Action<string, List<NetworkCharacter>> OnCharactersChanged { get; set; }

        Action<bool> OnNewGameSequenceStarted { get; set; }

        int SessionSeed { get; }

        int? LoadedSaveSeed { get; }

        int? AreaSeed { get; }

        int? CombatTurnSeed { get; }

        int? CombatSeed { get; }

        int? CrusadeArmyCombatAreaSeed { get; }

        int? CrusadeArmyCombatSeed { get; }

        bool IsControlledByLocalPlayer(string unitId);

        bool IsControlledByPlayers(string unitId);

        void OnAfterCueShow(string dialogName, string cueName, bool hasSystemAnswer);

        bool OnBeforeSelectDialogAnswer(string dialogName, string cueName, string answerName, bool isExitAnswer, string manualUnitSelectionId);

        bool StartDialog(string dialogName, string targetUnitId, string initiatorUnitId, string mapObjectId, string speakerKey);

        void UpdateCharactersOwnership();

        void CombatStarted();

        void CombatEnded();

        bool CanInitializeCombat();

        bool CanContinueCombat();

        bool OnBeforeTurnStart(string unitId, bool actingInSurpriseRound);

        bool OnBeforeTurnEnd(string unitId);

        void CombatRoundStarted(int round);

        void ForceLoadGame(string savePath, string gameId);

        bool IsDiceRollOwner();

        TRollValue RetrieveRoll<TRollValue>(int networkDiceRollId, string ruleName, string unitId)
            where TRollValue : RollValueBase;

        void OnClickUnit(NetworkClick click);

        void OnClickGround(NetworkClick click);

        void OnClickMapObject(NetworkClick click);

        void OnAbilityUse(NetworkAbilityUse abilityUse);

        void OnUnitAttackCommandStarted(NetworkUnitAttack networkUnitAttack);

        void OnToggleActivatableAbility(NetworkActivatableAbility activatableAbilityUse);

        void OnTransferInventoryItem(NetworkItemsTransfer transferItem);

        void OnSkinLootContainer(NetworkLootableEntity networkLootableEntity);

        void OnDropItem(NetworkDropItem dropItem);

        void OnEquipmentSlotChanged(NetworkEquipmentSlot networkSlot);

        void OnChangeActiveHandEquipmentSet(NetworkActiveHandEquipmentSet networkActiveHandEquipmentSet);

        void OnInteractWithMapObjectOvertip(NetworkOvertip networkOvertip);

        void OnAreaScenesLoaded();

        void OnAreaLoadingComplete();

        bool CanUnitJoinCombat(string unitId);

        string GetCharacterOwnerName(string unitId);

        void OnStartGameMode(GameModeType type);

        void OnStopGameMode(GameModeType type);

        void OnShowRestView(RestPhase phase);

        void OnInterrupRestBanterBark(NetworkRestBanter networkBanter);

        void OnTransferVendorItem(NetworkVendorItemTransfer transfer);

        void OnMemorizeSpell(string unitId, NetworkSpellSlot networkSpellSlot, NetworkAbility networkAbility);

        void OnForgetSpell(string unitId, NetworkSpellSlot networkSpellSlot, NetworkAbility networkAbility);

        void OnLevelingMythicClassSelected(string mythicClassId);

        void OnLevelingClassSelected(NetworkLevelingClass levelingClass);

        void OnLevelingClassArchetypeSelected(NetworkLevelingArchetype levelingArchetype);

        void OnLevelingTerminated();

        bool CanMakeLevelingDecisions();

        void OnLevelingWitnessPhase(NetworkLevelingPhase phase);

        void OnLevelingIncreaseSkillPoint(NetworkLevelingSkillPoint skill);

        void OnLevelingDecreaseSkillPoint(NetworkLevelingSkillPoint skill);

        void OnLevelingFeatureSelected(NetworkLevelingFeature feature);

        void OnLevelingSpellRemoved(NetworkLevelingSpell spell);

        void OnLevelingSpellChosen(NetworkLevelingSpell spell);

        void OnLevelingCompleted();

        void OnLevelingIncreaseAbilityScore(NetworkLevelingAbilityScore abilityScore);

        void OnLevelingDecreaseAbilityScore(NetworkLevelingAbilityScore abilityScore);

        void OnLevelingPortraitSelected(NetworkLevelingPortrait levelingPortrait);

        void OnLevelingVoiceSelected(NetworkLevelingVoice levelingVoice);

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

        bool OnRequestLevelingUI(string unitId, NetworkLevelingType networkLevelingType);

        void OnForceLevelingUI(string unitId, NetworkLevelingType networkLevelingType);

        void OnHandleDelayCombatTurn(string unitId, string targetUnitId);

        void OnSetUnitStealthEnabled(string unitId, bool isEnabled, bool isForced);

        void OnShowGroupChangerUI();

        bool OnClickGroupChangerUnit(string unitId);

        void OnSkipTimeOpened();

        bool OnGlobalMapSelectLocation(NetworkGlobalMapLocation globalMapLocation);

        void OnGlobalMapMessageBoxShown();

        void OnGlobalMapCommonPopupShown(NetworkGlobalMapCommonPopup globalMapCommonPopup);

        void OnGlobalMapEncounterMessageShown();

        void OnGlobalMapDisposed();

        void OnGlobalMapTravelerModeChanged(NetworkGlobalMapTravelerMode travelerMode);

        bool OnSpawnCampPlace(NetworkVector3 position);

        void OnZoneLootShown();

        void OnZoneLootClosed();

        void OnZoneLootCollectorButtonsUpdated();

        void OnDialogPopupShown(NetworkDialogPopup networkDialogPopup);

        void OnUseInventoryItem(NetworkUseInventoryItem useInventoryItem);

        void OnLevelingBodyTypeAppearanceChanged(int index);

        void OnLevelingFaceAppearanceChanged(int index);

        void OnLevelingScarAppearanceChanged(int index);

        void OnLevelingHairStyleAppearanceChanged(int index);

        void OnLevelingHornsAppearanceChanged(int index);

        void OnLevelingWarpaintAppearanceChanged(NetworkLevelingWarpaint levelingWarpaint);

        void OnLevelingTattooAppearanceChanged(NetworkLevelingTattoo levelingTattoo);

        void OnLevelingBodyColorAppearanceChanged(string textureName);

        void OnLevelingEyesColorAppearanceChanged(string textureName);

        void OnLevelingHairColorAppearanceChanged(string textureName);

        void OnLevelingHornsColorAppearanceChanged(string textureName);

        void OnLevelingWarpaintColorAppearanceChanged(NetworkLevelingWarpaint levelingWarpaint);

        void OnLevelingTattooColorAppearanceChanged(NetworkLevelingTattoo levelingTattoo);

        void OnLevelingPrimaryOutfitColorAppearanceChanged(string textureName);

        void OnLevelingSecondaryOutfitColorAppearanceChanged(string textureName);

        void OnLevelingRespecCompleted();

        void OnLevelingRespecWindowShown(string unitId);

        void OnLevelingRespecLevelUp();

        void OnLevelingRespecMythicLevelUp();

        void OnCharacterSelectionWindowShown();

        void OnNewGameSequenceWitnessPhase(NetworkNewGameSequencePhase newGameSequencePhase);

        string GetNewGameSequenceId();

        bool CanMakeNewGameSequenceDecisions();

        bool OnCreateAndEquipPolymorphInSlot(NetworkPolymorphicItem polymorphicItem);

        void OnCapitalModeRest();

        void OnStartRest();

        void OnStartRestSleepPhase();

        void OnGameLoaded();

        void OnPing(NetworkPing ping);

        void OnCutsceneSkip();

        bool OnTacticalCombatInitialization();

        void OnTacticalCombatEnded();

        bool OnBeforeTacticalCombatTurnStart(int turnNumber);

        void OnCrusadeArmyCombatTurnStarted(NetworkArmyCombatTurn armyCombatTurn);

        void OnCrusadeArmyBattleResultsShown();

        void OnGlobalMapCombatResultsShown();

        void OnGlobalMapCrusadeArmyInfoShown();

        void OnGlobalMapCrusadeArmyMergeCartClosed();

        void OnGlobalMapCrusadeArmyInfoMergeShown();

        void OnGlobalMapCrusadeArmySetLeaderShown();

        void OnGlobalMapCrusadeArmySetLeaderClosed();

        void OnGlobalMapCrusadeArmyBuyLeaderShown();

        void OnGlobalMapCrusadeArmyBuyLeaderClosed();

        void OnGlobalMapRecruitmentShown();

        void OnGlobalMapRecruitmentClosed();

        void OnGlobalMapRecruitmentSlotsRerolled();

        void OnGlobalMapCrusadeArmyLeaderLevelingShown();

        void OnGlobalMapCrusadeArmyLeaderLevelingStarted(NetworkGlobalMapArmy globalMapArmy);

        void OnUnitDeath(string unitId);

        void OnTrapDisarmRolled(NetworkTrapDisarm trapDisarm);

        void OnUnitAutoUseAbilityChanged(NetworkAutoUseAbility networkAutoUseAbility);

        void OnCopyInventoryItem(NetworkItemCopy itemCopy);

        bool OnAreaEffectTriggered(NetworkAreaEffect areaEffect);
    }
}
