using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Kingmaker.EntitySystem;
using Kingmaker.GameModes;
using Kingmaker.Items.Slots;
using Kingmaker.UI;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.GameInteraction;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Abilities;
using WOTRMultiplayer.MP.Entities.Dialogs;
using WOTRMultiplayer.MP.Entities.Equipment;
using WOTRMultiplayer.MP.Entities.Loot;
using WOTRMultiplayer.MP.Entities.MapObjects;

namespace WOTR.Multiplayer.Playground.Core.Dummies
{
    public class DummyGameInteractionService : IGameInteractionService
    {
        public bool IsPaused { get; set; }

        public NetworkExecutionContext ExecutionContext { get; }

        public GameModeType CurrentGameMode => GameModeType.None;

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

        public void MoveNonCombatCharacter(string unitId, NetworkVector3 destination, float delay, float orientation)
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

        public void StartTurnBasedCombatTurn(bool isActingInSurpriseRound)
        {
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

        public bool HasBeenTriggeredByAnotherPlayer(NetworkDropItem dropItem)
        {
            return false;
        }

        public bool HasBeenTriggeredByAnotherPlayer(NetworkEquipmentSlot networkSlot)
        {
            return false;
        }

        public bool HasBeenTriggeredByAnotherPlayer(NetworkActiveHandEquipmentSet set)
        {
            return false;
        }

        public void InteractWithOvertip(NetworkOvertip networkOvertip)
        {
        }

        public EntityDataBase GetEntity(string id)
        {
            return null;
        }
    }
}
