using System.Collections.Generic;
using Kingmaker.EntitySystem.Persistence;
using Kingmaker.GameModes;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.GameInteraction.Contexts;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Abilities;
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

        void MoveNonCombatCharacter(NetworkCharacterMove move);

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

        void OnClickUnit(NetworkClick click);

        void OnClickGround(NetworkClick click);

        void OnClickMapObject(NetworkClick click);

        void OnInteractWithMapObjectOvertip(NetworkOvertip networkOvertip);

        void OnAbilityUse(NetworkAbility ability);

        void OnToggleActivatableAbility(NetworkActivatableAbility ability);

        bool IsActive { get; }

        bool IsInCombat { get; }

        NetworkActionsState GetActionsState();

        void OnLootContainer(NetworkLootContainer container);

        void OnDropItem(NetworkDropItem dropItem);

        void OnChangeActiveHandEquipmentSet(NetworkActiveHandEquipmentSet set);

        bool CanUnitJoinCombat(string unitId);

        bool CanMakePerceptionCheck(string unitId, string mapObjectId);

        bool OnInspectionKnowledgeCheck(NetworkInspectionKnowledgeCheck check);

        void OnPerceptionCheck(NetworkPerceptionCheck check);

        bool OnSpawnCampPlace(NetworkVector3 position);

        bool OnCampingUseHealingSpellsChanged(bool isActive);

        void OnCampingUnitsRoleChanged(List<NetworkCampingRole> roles);

        void OnStartRest();

        bool CanUseCampingUI();

        void OnBeforeTryRollRandomEncounter();

        void OnAfterTryRollRandomEncounter();

        int? GetNextRestBanter(int minInclusive, int maxExclusive);

        void OnInterrupRestBanterBark(NetworkRestBanter networkBanter);

        NetworkAIAction OnAfterAISelectedAction(NetworkAIAction action);

        bool ShouldGroundHandlerMoveAllUnitsToPoint();

        void ResetExecutionContext();

        void OnTransferVendorItem(NetworkVendorItemTransfer transfer);

        bool CanFullyControlVendorUI();

        void OnMakeVendorDeal();

        void OnCloseVendorWindow();

        void OnMemorizeSpell(NetworkSpellSlot slot);

        void OnForgetSpell(NetworkSpellSlot slot);

        bool RequestLevelingUI(string unitId);

        void OnLevelingClassArchetypeSelected(string archetypeId);

        void OnLevelingClassSelected(string classId);

        void OnLevelingTerminated();

        bool CanMakeLevelingDecisions();

        void OnWitnessLevelingPhase(NetworkLevelingPhase phase);

        void OnLevelingIncreaseSkillPoint(NetworkLevelingSkillPoint skill);

        void OnLevelingDecreaseSkillPoint(NetworkLevelingSkillPoint skill);

        void OnLevelingFeatureSelected(NetworkLevelingFeature feature);

        void OnLevelingSpellRemoved(NetworkLevelingSpell spell);

        void OnLevelingSpellChosen(NetworkLevelingSpell spell);

        void OnLevelingCompleted();

        void OnLevelingIncreaseAbilityScore(NetworkLevelingAbilityScore abilityScore);

        void OnLevelingDecreaseAbilityScore(NetworkLevelingAbilityScore abilityScore);
    }
}
