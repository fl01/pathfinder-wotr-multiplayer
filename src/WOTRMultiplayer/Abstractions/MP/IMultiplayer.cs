using System.Collections.Generic;
using Kingmaker.EntitySystem.Persistence;
using Kingmaker.GameModes;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.GameInteraction.Contexts;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.ActionBar;
using WOTRMultiplayer.MP.Entities.Combat;
using WOTRMultiplayer.MP.Entities.Equipment;
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

        bool CanLootUnit(string initiatorUnitId);

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

        NetworkActionsState GetActionsState();

        void OnLootContainer(NetworkLootContainer networkLootContainer);

        void OnSkinLootContainer(NetworkLootContainer container);

        void OnDropItem(NetworkDropItem networkDropItem);

        void OnChangeActiveHandEquipmentSet(NetworkActiveHandEquipmentSet networkActiveHandEquipmentSet);

        bool CanUnitJoinCombat(string unitId);

        bool CanMakePerceptionCheck(string unitId, string mapObjectId);

        bool OnInspectionKnowledgeCheck(NetworkInspectionKnowledgeCheck networkInspectionKnowledgeCheck);

        void OnPerceptionCheck(NetworkPerceptionCheck networkPerceptionCheck);

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

        bool RequestLevelingUI(string unitId);

        void OnLevelingClassArchetypeSelected(string archetypeId);

        void OnLevelingClassSelected(string classId);

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

        void OnMoveActionBarSlot(NetworkActionBarSlot sourceActionBarSlot, NetworkActionBarSlot targetActionBarSlot);

        void OnClearActionBarSlot(NetworkActionBarSlot actionBarSlot);

        bool CanTogglePause(bool isPaused);

        void OnAutoPausedByTrapDetection();

        void OnLockpickInteraction(NetworkLockpickInteraction lockpickInteraction);

        void OnUnitAttack(NetworkUnitAttack networkUnitAttack);
    }
}
