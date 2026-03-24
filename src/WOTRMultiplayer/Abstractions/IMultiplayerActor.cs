using System;
using System.Collections.Generic;
using Kingmaker.Controllers.Rest;
using Kingmaker.GameModes;
using Kingmaker.UI.Kingdom;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.ActionBar;
using WOTRMultiplayer.Entities.Area;
using WOTRMultiplayer.Entities.AreaEffects;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.Combat.Crusades;
using WOTRMultiplayer.Entities.Dialogs;
using WOTRMultiplayer.Entities.Equipment;
using WOTRMultiplayer.Entities.GlobalMap;
using WOTRMultiplayer.Entities.GlobalMap.Kingdom;
using WOTRMultiplayer.Entities.Items;
using WOTRMultiplayer.Entities.Leveling;
using WOTRMultiplayer.Entities.MapObjects;
using WOTRMultiplayer.Entities.NewGame;
using WOTRMultiplayer.Entities.Ping;
using WOTRMultiplayer.Entities.Rest;
using WOTRMultiplayer.Entities.SpellbookManagement;
using WOTRMultiplayer.Entities.Spells;
using WOTRMultiplayer.Entities.Vendor;
using WOTRMultiplayer.Services.Random;

namespace WOTRMultiplayer.Abstractions
{
    public interface IMultiplayerActor
    {
        NetworkGameConnectivity GetGameConnectivity();

        List<NetworkPlayer> GetPlayers();

        List<NetworkPlayer> GetOtherPlayers();

        List<NetworkCharacter> GetCharacters();

        bool ReadyChanged();

        NetworkArea CurrentArea { get; }

        bool IsInCombat { get; }

        bool IsActive { get; }

        bool IsInLobby { get; }

        void Reset();

        Action<NetworkGameConnectivity> OnConnected { get; set; }

        Action<NetworkLobbyStage, List<NetworkPlayer>> OnPlayersChanged { get; set; }

        Action<string, List<NetworkCharacter>> OnCharactersChanged { get; set; }

        Action<bool> OnNewGameSequenceStarted { get; set; }

        Action<Dictionary<long, float>> OnSaveGameTransferProgressChanged { get; set; }

        Action OnGameStarted { get; set; }

        SeededContext GetSeededContext(SeedKind seedKind = SeedKind.All);

        int? CrusadeArmyCombatAreaSeed { get; }

        bool IsControlledByLocalPlayer(string unitId);

        bool IsControlledByPlayers(string unitId);

        void OnAfterCueShow(NetworkDialog networkDialog, string cueName, bool hasSystemAnswer);

        bool OnBeforeSelectDialogAnswer(NetworkDialog networkDialog, string cueName, string answerName, bool isExitAnswer, string manualUnitSelectionId);

        bool StartDialog(NetworkDialog networkDialog);

        void UpdateCharactersOwnership();

        void CombatStarted();

        void CombatEnded();

        bool CanInitializeCombat();

        bool CanContinueCombat();

        bool OnBeforeTurnStart(string unitId, bool actingInSurpriseRound);

        bool OnBeforeTurnEnd(string unitId);

        void CombatRoundStarted(int round);

        void ForceLoadGame(string savePath, string gameId);

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

        void OnAreaLoadingComplete();

        void OnAreaLoaded();

        bool CanUnitJoinCombat(string unitId, string groupId);

        string GetCharacterOwnerName(string unitId);

        void OnStartGameMode(GameModeType type);

        void OnStopGameMode(GameModeType type);

        void OnShowRestView(RestPhase phase);

        void OnInterrupRestBanterBark(NetworkRestBanter networkBanter);

        void OnTransferVendorItem(NetworkVendorItemTransfer transfer);

        void OnMemorizeSpell(string unitId, NetworkSpellSlot networkSpellSlot, NetworkAbility networkAbility);

        void OnForgetSpell(string unitId, NetworkSpellSlot networkSpellSlot, NetworkAbility networkAbility);

        void OnSwapMemorizedSlots(string unitId, string spellbookId, int spellLevel, NetworkSpellSlot spellSlotA, NetworkSpellSlot spellSlotB);

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

        void OnGlobalMapMessageBoxShown(bool fromClick);

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

        void OnUnitDeath(string unitId, string groupId);

        void OnTrapDisarmRolled(NetworkTrapDisarm trapDisarm);

        void OnTrapActivation(string unitId, NetworkMapObject trapObject);

        void OnUnitAutoUseAbilityChanged(NetworkAutoUseAbility networkAutoUseAbility);

        void OnCopyInventoryItem(NetworkItemCopy itemCopy);

        void OnItemDescriptionRead(NetworkItem networkItem);

        bool OnAreaEffectTriggered(NetworkAreaEffect areaEffect);

        NetworkAIAction OnAfterAISelectedAction(NetworkAIAction networkAIAction);

        void OnUnitMoveTo(NetworkUnitMoveTo unitMoveTo);

        bool CanLeaveCombat();

        void OnEnterKingdom(NetworkKingdomEntryPoint kingdomEntryPoint);

        void OnExitKingdom();

        void OnKingdomLoaded();

        void OnKingdomUnloaded();

        void ForceUnpause();

        void OnKingdomNavigationChanged(KingdomNavigationType kingdomNavigationType);

        void OnKingdomSettlementLoaded();

        void OnTransitionMapShown();

        void OnSpellbookMetamagicSpellCreated(NetworkMetamagicSpell metamagicSpell);

        void OnRemoveCustomSpell(string unitId, NetworkAbility ability);

        void OnUnitInteractWithUnit(NetworkUnitInteractWithUnit networkUnitInteractWithUnit);

        void OnUnitLootUnit(NetworkUnitLootUnit networkUnitLootUnit);

        void OnMapObjectCombinePartInteraction(NetworkMapObject mapObject, string interactedUnitId, int partIndex);

        void ForceCombatEnd();
    }
}
