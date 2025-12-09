using System;
using System.Collections.Generic;
using Kingmaker.Controllers.Rest;
using Kingmaker.GameModes;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.ActionBar;
using WOTRMultiplayer.MP.Entities.Combat;
using WOTRMultiplayer.MP.Entities.Equipment;
using WOTRMultiplayer.MP.Entities.GlobalMap;
using WOTRMultiplayer.MP.Entities.Leveling;
using WOTRMultiplayer.MP.Entities.Loot;
using WOTRMultiplayer.MP.Entities.MapObjects;
using WOTRMultiplayer.MP.Entities.Movement;
using WOTRMultiplayer.MP.Entities.Rest;
using WOTRMultiplayer.MP.Entities.Rolls.Claiming.Values;
using WOTRMultiplayer.MP.Entities.Spells;
using WOTRMultiplayer.MP.Entities.Vendor;

namespace WOTRMultiplayer.Abstractions.MP.Actors
{
    public interface IMultiplayerActor
    {
        long GetLocalPlayerId();

        NetworkGameConnectivity GetGameConnectivity();

        List<NetworkPlayer> GetPlayers();

        List<NetworkPlayer> GetOtherPlayers();

        List<NetworkCharacter> GetCharacters();

        bool ReadyChanged();

        void MoveNonCombatCharacter(NetworkCharacterMove move);

        bool IsInCombat { get; }

        bool IsActive { get; }

        bool IsInLobby { get; }

        void Reset();

        Action<NetworkGameConnectivity> OnConnected { get; set; }

        Action<List<NetworkPlayer>> OnPlayersChanged { get; set; }

        int SessionSeed { get; }

        int? CombatSeed { get; }

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

        bool OnBeforeStartTurn(string unitId, bool actingInSurpriseRound);

        bool OnBeforeEndTurn(string unitId);

        void CombatRoundStarted(int round);

        void ForceLoadGame(string savePath, string gameId);

        bool IsDiceRollOwner();

        TRollValue RetrieveRoll<TRollValue>(int networkDiceRollId, string unitId)
            where TRollValue : RollValueBase;

        void OnClickUnit(NetworkClick click);

        void OnClickGround(NetworkClick click);

        void OnClickMapObject(NetworkClick click);

        void OnAbilityUse(NetworkAbility ability);

        void OnUnitAttack(NetworkUnitAttack networkUnitAttack);

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

        string GetMultiplayerOwnerName(string unitId);

        bool OnStartGameMode(GameModeType type);

        bool OnStopGameMode(GameModeType type);

        bool OnShowRestView(RestPhase phase);

        void OnInterrupRestBanterBark(NetworkRestBanter networkBanter);

        NetworkAIAction OnAfterAISelectedAction(NetworkAIAction action);

        void OnTransferVendorItem(NetworkVendorItemTransfer transfer);

        void OnMemorizeSpell(NetworkSpellSlot slot);

        void OnForgetSpell(NetworkSpellSlot slot);

        void OnLevelingClassSelected(string classId);

        void OnLevelingClassArchetypeSelected(string archetypeId);

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

        void OnMoveActionBarSlot(NetworkActionBarSlot sourceActionBarSlot, NetworkActionBarSlot targetActionBarSlot);

        void OnClearActionBarSlot(NetworkActionBarSlot actionBarSlot);

        bool TogglePause(bool isPaused);

        void OnAutoPausedByTrapDetection();

        void OnLockpickInteraction(NetworkLockpickInteraction lockpickInteraction);

        bool OnRequestLevelingUI(string unitId);

        void OnHandleDelayCombatTurn(string unitId, string targetUnitId);

        void OnSetUnitStealthEnabled(string unitId, bool isEnabled, bool isForced);

        void OnShowGroupChangerUI();

        bool OnClickGroupChangerUnit(string unitId);

        void OnSkipTimeOpened();

        bool OnGlobalMapSelectLocation(NetworkGlobalMapLocation globalMapLocation);

        void OnGlobalMapMessageBoxShown();

        void OnGlobalMapMessageBoxClosed();

        void OnGlobalMapIngredientCollectionShown();

        void OnGlobalMapIngredientCollectionClosed();

        void OnGlobalMapEncounterMessageShown();

        bool OnSpawnCampPlace(NetworkVector3 position);

        void OnZoneLootShown();

        void OnZoneLootClosed();

        void OnZoneLootCompleted();

        void OnZoneLootCollectorButtonsUpdated();
    }
}
