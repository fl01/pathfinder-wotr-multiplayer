using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Kingmaker.EntitySystem;
using Kingmaker.GameModes;
using Kingmaker.Items.Slots;
using Kingmaker.UI;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.GameInteraction.Contexts;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Abilities;
using WOTRMultiplayer.MP.Entities.Dialogs;
using WOTRMultiplayer.MP.Entities.Equipment;
using WOTRMultiplayer.MP.Entities.Inspect;
using WOTRMultiplayer.MP.Entities.Leveling;
using WOTRMultiplayer.MP.Entities.Loot;
using WOTRMultiplayer.MP.Entities.MapObjects;
using WOTRMultiplayer.MP.Entities.Movement;
using WOTRMultiplayer.MP.Entities.Rest;
using WOTRMultiplayer.MP.Entities.Settings;
using WOTRMultiplayer.MP.Entities.Spells;
using WOTRMultiplayer.MP.Entities.Vendor;

namespace WOTR.Multiplayer.Playground.Core.Dummies
{
    public class DummyGameInteractionService : IGameInteractionService
    {
        public bool IsPaused { get; set; }

        public RemoteExecutionContext RemoteContext { get; }

        public GameModeType CurrentGameMode => GameModeType.None;

        public string CampingPotionBlueprintRecipeId => null;

        public string CampingCookingBlueprintRecipeId => null;

        public string CampingScrollBlueprintRecipeId => null;

        public bool CampingAutotuneIterationsStatus => false;

        public int CampingIterationsCount => 0;

        public bool IsRandomEncounter => false;

        public string GetSaveGamePath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var fullPath = Path.Combine(appData, "AppData\\LocalLow\\Owlcat Games\\Pathfinder Wrath Of The Righteous\\Saved Games\\");
            return fullPath;
        }

        public bool IsUnitAI(string unitId)
        {
            return true;
        }

        public List<NetworkCharacterOwnership> GetPartyPlayers()
        {
            return [];
        }

        public List<NetworkUnit> GetUnitsInCombat()
        {
            return [];
        }

        public void LeaveArea(string areaExitId)
        {
        }

        public void MarkSuggestedDialogAnswers(List<NetworkDialogAnswerSuggestion> suggestions)
        {
        }

        public void Pause(bool isPaused)
        {
        }

        public void PlaySound(UISoundType type)
        {
        }

        public void SelectDialogAnswer(string dialogName, string cueName, string answerName, string manualUnitSelectionId)
        {
        }

        public void SetDialogContinueButtonState(bool isEnabled)
        {
        }

        public void ShowModalMessage(string error)
        {
        }

        public Task<bool> StartDialogAsync(string dialogName, string targetUnitId, string initiatorUnitId, string mapObjectId, string speakerKey)
        {
            return Task.FromResult(true);
        }

        public string QuickLoadGame(string savePath)
        {
            return "1";
        }

        public void LoadGameFromMainMenu(string savePath)
        {
        }

        public string GetPetOwnerId(string unitId)
        {
            return null;
        }

        public void EndTurnBasedCombatTurn()
        {
        }

        public Task UpdateUnitsAsync(List<NetworkUnit> networkUnits)
        {
            return Task.CompletedTask;
        }

        public void ClickUnit(NetworkClick click)
        {
        }

        public void ClickGroundInCombat(NetworkClick click)
        {
        }

        public bool CombatTurnHasBeenFinished()
        {
            return true;
        }

        public NetworkActionsState GetActionsState()
        {
            return null;
        }

        public void UseAbility(NetworkAbility use)
        {
        }

        public void ToggleActivatableAbility(NetworkActivatableAbility toggle)
        {
        }

        public void ClickMapObject(NetworkClick click)
        {
        }

        public void CollectContainerLoot(NetworkLootContainer container)
        {
        }

        public void DropItem(NetworkDropItem dropItem)
        {
        }

        public NetworkEquipmentSlotPosition GetEquipmentSlotPosition(ItemSlot slot)
        {
            return null;
        }

        public void UpdateEquipmentSlot(NetworkEquipmentSlot slot)
        {
        }

        public void SetActiveHandEquipmentSet(NetworkActiveHandEquipmentSet set)
        {
        }

        public void InteractWithOvertip(NetworkOvertip networkOvertip)
        {
        }

        public EntityDataBase GetEntity(string id)
        {
            return null;
        }

        public bool IsSummoned(string unitId)
        {
            return false;
        }

        public void ResetSuggestedDialogAnswers()
        {
        }

        public void ApplyPerceptionCheck(NetworkPerceptionCheck check)
        {
        }

        public void UpdateCombatOrder(List<string> combatOrderUnits)
        {
        }

        public List<string> GetUnitsCombatOrder()
        {
            return [];
        }

        public NetworkGameSettings GetGameSettings()
        {
            return null;
        }

        public void ApplyGameSettings(NetworkGameSettings gameSettings)
        {
        }

        public void ShowWarningNotification(string text)
        {
        }

        public void SpawnCampPlace(NetworkVector3 position)
        {
        }

        public void SetCampingUseHealingSpells(bool isOn)
        {
        }

        public void SetCampingState(NetworkCampingState state)
        {
        }

        public void SetCampingRoles(List<NetworkCampingRole> roles)
        {
        }

        public void SetStartRestButtonState(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }

        public void StartRest()
        {
        }

        public void SetRandomEncounterContext(NetworkRandomEncounterContext context)
        {
        }

        public string GetNextUnitTurn()
        {
            return null;
        }

        public void SetNextUnitCombatTurn(string nextUnitTurn)
        {
        }

        public void ApplyInspectionKnowledgeCheck(NetworkInspectionKnowledgeCheck check)
        {
        }

        public void UpdateIsInCombatStatus()
        {
        }

        public void TryInterruptRestBanter(NetworkRestBanter banter)
        {
        }

        public void StartTurnBasedCombatTurn(string unitId)
        {
        }

        public void SetGroundMoveEveryone()
        {
        }

        public void AddCombatText(string text)
        {
        }

        public void TransferVendorItem(NetworkVendorItemTransfer transfer)
        {
        }

        public void CloseVendorWindow()
        {
        }

        public void MakeVendorDeal()
        {
        }

        public void ForgetSpell(NetworkSpellSlot slot)
        {
        }

        public void MemorizeSpell(NetworkSpellSlot slot)
        {
        }

        public void StartLeveling(string unitId)
        {
        }

        public void SelectLevelingClassArchetype(string archetypeId)
        {
        }

        public void SelectLevelingClass(string classId)
        {
        }

        public void UpdateLevelingPhaseControls(bool isEnabled)
        {
        }

        public void SwitchLevelingPhase(NetworkLevelingPhase phase)
        {
        }

        public void DecreaseLevelingSkillPoint(NetworkLevelingSkillPoint skillPoint)
        {
        }

        public void IncreaseLevelingSkillPoint(NetworkLevelingSkillPoint skillPoint)
        {
        }

        public void SelectLevelingFeature(NetworkLevelingFeature feature)
        {
        }

        public void SelectLevelingSpell(NetworkLevelingSpell spell)
        {
        }

        public void RemoveLevelingSpell(NetworkLevelingSpell spell)
        {
        }

        public void CompleteLeveling()
        {
        }

        public void TerminateLeveling()
        {
        }

        public void DecreaseLevelingAbilityScore(NetworkLevelingAbilityScore abilityScore)
        {
        }

        public void IncreaseLevelingAbilityScore(NetworkLevelingAbilityScore abilityScore)
        {
        }

        public void MoveNonCombatCharacter(NetworkCharacterMove move)
        {
        }
    }
}
