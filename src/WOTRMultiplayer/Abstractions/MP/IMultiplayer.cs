using System.Collections.Generic;
using Kingmaker.EntitySystem.Persistence;
using Kingmaker.GameModes;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.GameInteraction.Contexts;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Abilities;
using WOTRMultiplayer.MP.Entities.Equipment;
using WOTRMultiplayer.MP.Entities.Loot;
using WOTRMultiplayer.MP.Entities.MapObjects;
using WOTRMultiplayer.MP.Entities.Rest;

namespace WOTRMultiplayer.Abstractions.MP
{
    public interface IMultiplayer
    {
        NetworkExecutionContext ExecutionContext { get; }

        IUIFactory Factory { get; }

        IUniqueIdGenerator IdGenerator { get; }

        bool InitializeMultiplayer(InitializeMultiplayerContext context);

        void TerminateMultiplayer();

        void InitializeEscMenuLobbyWindow(InitializeEscMenuLobbyWindowContext context);

        void MoveNonCombatCharacter(string unitId, NetworkVector3 destination, float delay, float orientation);

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

        void OnPerceptionCheck(NetworkPerceptionCheck check);

        bool OnSpawnCampPlace(NetworkVector3 position);

        bool OnCampingUseHealingSpellsChanged(bool isOn);

        void OnCampingUnitsRoleChanged(List<NetworkCampingRole> roles);

        bool CanUseCampingUI();
    }
}
