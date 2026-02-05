using System;
using System.Collections.Generic;
using System.IO;
using Kingmaker.EntitySystem;
using Kingmaker.GameModes;
using Kingmaker.Items.Slots;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.ActionBar;
using WOTRMultiplayer.Entities.Area;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.Content;
using WOTRMultiplayer.Entities.Equipment;
using WOTRMultiplayer.Entities.Inspect;
using WOTRMultiplayer.Entities.Items;
using WOTRMultiplayer.Entities.MapObjects;
using WOTRMultiplayer.Entities.Movement;
using WOTRMultiplayer.Entities.NewGame;
using WOTRMultiplayer.Entities.Ping;
using WOTRMultiplayer.Entities.Rest;
using WOTRMultiplayer.Entities.Settings;
using WOTRMultiplayer.Entities.Spells;
using WOTRMultiplayer.Entities.Vendor;
using WOTRMultiplayer.Services.GameInteraction.Contexts;

namespace WOTRMultiplayer.Playground.Core.Dummies
{
    public class DummyGameInteractionService : IGameInteractionService
    {
        public RemoteExecutionContext RemoteContext => null;

        public GameModeType CurrentGameMode => GameModeType.None;

        public void AcceptCharacterSelectionWindow()
        {
        }

        public void AcceptGlobalMapEncounter()
        {
        }

        public void AcceptGroupChangerParty()
        {
        }

        public void ApplyGameSettings(NetworkGameSettings networkGameSettings)
        {
        }

        public void ApplyInspectionKnowledgeCheck(NetworkInspectionKnowledgeCheck networkInspectionKnowledgeCheck)
        {
        }

        public void ApplyPerceptionCheck(NetworkPerceptionCheck networkPerceptionCheck)
        {
        }

        public void ApplyStealthPerceptionCheck(NetworkStealthPerceptionCheck networkStealthPerceptionCheck)
        {
        }

        public void ChangeUnitStealth(string unitId, bool isEnabled, bool isForced)
        {
        }

        public void ClearActionBarSlot(NetworkActionBarSlot actionBarSlot)
        {
        }

        public void ClickGroundInCombat(NetworkClick networkClick)
        {
        }

        public void ClickGroupChangerUnit(string unitId)
        {
        }

        public void ClickMapObject(NetworkClick networkClick)
        {
        }

        public void ClickUnit(NetworkClick networkClick)
        {
        }

        public void CloseCharacterSelectionWindow()
        {
        }

        public void CloseGroupChangerUI()
        {
        }

        public void CloseSkipTimeUI()
        {
        }

        public void CloseVendorWindow()
        {
        }

        public bool CombatTurnHasBeenFinished()
        {
            return false;
        }

        public void CompleteZoneLoot()
        {
        }

        public void CreateAndEquipPolymorphicItem(NetworkPolymorphicItem polymorphicItem, bool createContext)
        {
        }

        public void DropItem(NetworkDropItem networkDropItem)
        {
        }

        public void EndTurnBasedCombatTurn()
        {
        }

        public void ForgetSpell(NetworkSpellSlot networkSpellSlot)
        {
        }

        public NetworkCampingState GetCampigState()
        {
            return null;
        }

        public NetworkCombatState GetCombatState()
        {
            return null;
        }

        public string GetCurrentAreaName()
        {
            return string.Empty;
        }

        public int GetCurrentChapter()
        {
            return -1;
        }

        public EntityDataBase GetEntity(string id)
        {
            return null;
        }

        public NetworkEquipmentSlotPosition GetEquipmentSlotPosition(ItemSlot slot)
        {
            return null;
        }

        public NetworkGameSettings GetGameSettings()
        {
            return null;
        }

        public NetworkContentState GetInstalledContent()
        {
            return new NetworkContentState()
            {
                GameVersion = "Playground"
            };
        }

        public List<NetworkCharacter> GetPartyPlayers()
        {
            return [];
        }

        public string GetPetOwnerId(string unitId)
        {
            return string.Empty;
        }

        public NetworkPing GetPing()
        {
            return null;
        }

        public string GetSaveGamePath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var fullPath = Path.Combine(appData, "AppData\\LocalLow\\Owlcat Games\\Pathfinder Wrath Of The Righteous\\Saved Games");
            return fullPath;
        }

        public string GetUnitCharacterName(string unitId)
        {
            return string.Empty;
        }

        public bool HasAnyRunningCombatCommands()
        {
            return false;
        }

        public void InteractWithOvertip(NetworkOvertip networkOvertip)
        {
        }

        public bool IsInCombat()
        {
            return false;
        }

        public bool IsSummoned(string unitId)
        {
            return false;
        }

        public bool IsUnitInParty(string unitId)
        {
            return false;
        }

        public void LeaveArea(NetworkAreaTransition areaTransition)
        {
            throw new NotImplementedException();
        }

        public string LoadGameFromMainMenu(string savePath)
        {
            return string.Empty;
        }

        public void LockpickMapObject(NetworkLockpickInteraction lockpickInteraction)
        {
        }

        public void MakeVendorDeal()
        {
        }

        public void MemorizeSpell(NetworkSpellSlot networkSpellSlot)
        {
        }

        public void MoveActionBarSlots(NetworkActionBarSlot sourceActionBarSlot, NetworkActionBarSlot targetActionBarSlot)
        {
        }

        public void MoveNonCombatCharacter(NetworkCharacterMove networkCharacterMove)
        {
        }

        public void OpenGlobalMapRestMenu()
        {
        }

        public void OpenSkipTimeUI()
        {
        }

        public string QuickLoadGame(string savePath)
        {
            return string.Empty;
        }

        public void ReselectSelectedCharacters()
        {
        }

        public void ResetSuggestedDialogAnswers()
        {
        }

        public void SelectNewGameDifficulty(string difficulty)
        {
        }

        public void SelectNewGameSequencePhase(NetworkNewGameSequencePhase phase)
        {
        }

        public void SetActiveHandEquipmentSet(NetworkActiveHandEquipmentSet networkActiveHandEquipmentSet)
        {
        }

        public void SetCampingRoles(List<NetworkCampingRole> networkCampingRoles)
        {
        }

        public void SetCampingState(NetworkCampingState networkCampingState)
        {
        }

        public void SetCampingUseHealingSpells(bool isOn)
        {
        }

        public void SetPause(bool isPaused)
        {
        }

        public void SetRandomEncounterContext(NetworkRandomEncounterContext networkRandomEncounterContext)
        {
        }

        public void SkinLootContainer(NetworkLootableEntity networkLootableEntity)
        {
        }

        public void SkipCutscene(string playerName)
        {
        }

        public void SpawnCampPlace(NetworkVector3 position)
        {
        }

        public void StartNewGameSequence(string mainCharacterId, Action onBack, Action onStart, Action<NetworkCharacter> onCharacterCreated)
        {
        }

        public void StartNewGameSequenceLeveling()
        {
        }

        public void StartRest()
        {
        }

        public void StartSkipTime()
        {
        }

        public void TerminateNewGameSequence()
        {
        }

        public void ToggleActivatableAbility(NetworkActivatableAbility networkActivatableAbility)
        {
        }

        public void ToggleCharacterSelectionWindow(string unitId)
        {
        }

        public void TransferInventoryItems(NetworkItemsTransfer networkItemsTransfer)
        {
        }

        public void TransferVendorItem(NetworkVendorItemTransfer networkVendorItemTransfer)
        {
        }

        public void TryInterruptRestBanter(NetworkRestBanter networkRestBanter)
        {
        }

        public void UpdateCharacterSelectionUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }

        public void UpdateEquipmentSlot(NetworkEquipmentSlot networkEquipmentSlot)
        {
        }

        public void UpdateGroupChangerUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }

        public void UpdateIsInCombatStatus()
        {
        }

        public void UpdateNewGameSequencePhaseControls(bool isEnabled, NetworkNewGameSequencePhaseType newGameSequencePhaseType)
        {
        }

        public void UpdateSkipTimeHours(float hours)
        {
        }

        public void UpdateSkipTimeUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }

        public void UpdateRestUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }

        public void UpdateZoneLootRemoveToggle(bool removeLoot)
        {
        }

        public void UpdateZoneLootUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }

        public void UseInventoryItem(NetworkUseInventoryItem useInventoryItem)
        {
        }

        public void CloseRestWindow()
        {
        }

        public void InitiateRest()
        {
        }

        public void LeaveZoneLoot()
        {
        }

        public void ApplyTrapDisarm(NetworkTrapDisarm trapDisarm)
        {
        }

        public bool UnitIsBusy(string unitId)
        {
            return false;
        }
    }
}
