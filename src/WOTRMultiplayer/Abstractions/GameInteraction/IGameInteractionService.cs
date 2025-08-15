using System.Collections.Generic;
using System.Threading.Tasks;
using Kingmaker.EntitySystem;
using Kingmaker.GameModes;
using Kingmaker.Items.Slots;
using Kingmaker.UI;
using WOTRMultiplayer.GameInteraction.Contexts;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Abilities;
using WOTRMultiplayer.MP.Entities.Dialogs;
using WOTRMultiplayer.MP.Entities.Equipment;
using WOTRMultiplayer.MP.Entities.Inspect;
using WOTRMultiplayer.MP.Entities.Loot;
using WOTRMultiplayer.MP.Entities.MapObjects;
using WOTRMultiplayer.MP.Entities.Rest;
using WOTRMultiplayer.MP.Entities.Settings;
using WOTRMultiplayer.MP.Entities.Spells;
using WOTRMultiplayer.MP.Entities.Vendor;

namespace WOTRMultiplayer.Abstractions.GameInteraction
{
    public interface IGameInteractionService
    {
        RemoteExecutionContext RemoteContext { get; }

        bool IsPaused { get; }

        GameModeType CurrentGameMode { get; }

        string CampingPotionBlueprintRecipeId { get; }

        string CampingCookingBlueprintRecipeId { get; }

        string CampingScrollBlueprintRecipeId { get; }

        bool CampingAutotuneIterationsStatus { get; }

        int CampingIterationsCount { get; }

        bool IsRandomEncounter { get; }

        void LeaveArea(string areaExitId);

        void MarkSuggestedDialogAnswers(List<NetworkDialogAnswerSuggestion> suggestions);

        void ResetSuggestedDialogAnswers();

        void MoveNonCombatCharacter(string unitId, NetworkVector3 destination, float delay, float orientation);

        void Pause(bool isPaused);

        void SelectDialogAnswer(string dialogName, string cueName, string answerName, string manualUnitSelectionId);

        void SetDialogContinueButtonState(bool isEnabled);

        void PlaySound(UISoundType type);

        Task<bool> StartDialogAsync(string dialogName, string targetUnitId, string initiatorUnitId, string mapObjectId, string speakerKey);

        List<NetworkCharacterOwnership> GetPartyPlayers();

        void ShowModalMessage(string error);

        void ShowWarningNotification(string text);

        void AddCombatText(string text);

        bool IsUnitAI(string unitId);

        List<NetworkUnit> GetUnitsInCombat();

        string QuickLoadGame(string savePath);

        void LoadGameFromMainMenu(string savePath);

        string GetSaveGamePath();

        string GetPetOwnerId(string unitId);

        void StartTurnBasedCombatTurn(string unitId);

        void EndTurnBasedCombatTurn();

        Task UpdateUnitsAsync(List<NetworkUnit> networkUnits);

        void ClickUnit(NetworkClick click);

        void ClickGroundInCombat(NetworkClick click);

        void ClickMapObject(NetworkClick click);

        bool CombatTurnHasBeenFinished();

        NetworkActionsState GetActionsState();

        void UseAbility(NetworkAbility use);

        void ToggleActivatableAbility(NetworkActivatableAbility toggle);

        void CollectContainerLoot(NetworkLootContainer container);

        void DropItem(NetworkDropItem dropItem);

        NetworkEquipmentSlotPosition GetEquipmentSlotPosition(ItemSlot slot);

        void UpdateEquipmentSlot(NetworkEquipmentSlot slot);

        void SetActiveHandEquipmentSet(NetworkActiveHandEquipmentSet set);

        void InteractWithOvertip(NetworkOvertip networkOvertip);

        EntityDataBase GetEntity(string id);

        bool IsSummoned(string unitId);

        void ApplyPerceptionCheck(NetworkPerceptionCheck check);

        void ApplyInspectionKnowledgeCheck(NetworkInspectionKnowledgeCheck check);

        void UpdateCombatOrder(List<string> combatOrderUnits);

        List<string> GetUnitsCombatOrder();

        NetworkGameSettings GetGameSettings();

        void ApplyGameSettings(NetworkGameSettings gameSettings);

        void SpawnCampPlace(NetworkVector3 position);

        void SetCampingUseHealingSpells(bool isOn);

        void SetCampingState(NetworkCampingState state);

        void SetCampingRoles(List<NetworkCampingRole> roles);

        void SetStartRestButtonState(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void StartRest();

        void SetRandomEncounterContext(NetworkRandomEncounterContext context);

        string GetNextUnitTurn();

        void SetNextUnitCombatTurn(string nextUnitTurn);

        void UpdateIsInCombatStatus();

        void TryInterruptRestBanter(NetworkRestBanter banter);

        void SetGroundMoveEveryone();

        void TransferVendorItem(NetworkVendorItemTransfer transfer);

        void CloseVendorWindow();

        void MakeVendorDeal();
        void ForgetSpell(NetworkSpellSlot slot);
        void MemorizeSpell(NetworkSpellSlot slot);
    }
}
