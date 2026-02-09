using System;
using System.Collections.Generic;
using Kingmaker.EntitySystem;
using Kingmaker.GameModes;
using Kingmaker.Items.Slots;
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
using WOTRMultiplayer.Entities.Rest;
using WOTRMultiplayer.Entities.Settings;
using WOTRMultiplayer.Entities.Spells;
using WOTRMultiplayer.Entities.Vendor;
using WOTRMultiplayer.Services.GameInteraction.Contexts;

namespace WOTRMultiplayer.Abstractions.GameInteraction
{
    public interface IGameInteractionService
    {
        RemoteExecutionContext RemoteContext { get; }

        GameModeType CurrentGameMode { get; }

        void LeaveArea(NetworkAreaTransition areaTransition);

        void MoveNonCombatCharacter(NetworkCharacterMove networkCharacterMove);

        void SetPause(bool isPaused);

        List<NetworkCharacter> GetPartyPlayers();

        bool IsUnitInParty(string unitId);

        string QuickLoadGame(string savePath);

        string LoadGameFromMainMenu(string savePath);

        string GetSaveGamePath();

        string GetPetOwnerId(string unitId);

        void ClickUnit(NetworkClick networkClick);

        void ClickMapObject(NetworkClick networkClick);

        void ClickGroundInCombat(NetworkClick networkClick);

        void ToggleActivatableAbility(NetworkActivatableAbility networkActivatableAbility);

        void TransferInventoryItems(NetworkItemsTransfer networkItemsTransfer);

        void SkinLootContainer(NetworkLootableEntity networkLootableEntity);

        void DropItem(NetworkDropItem networkDropItem);

        NetworkEquipmentSlotPosition GetEquipmentSlotPosition(ItemSlot slot);

        void UpdateEquipmentSlot(NetworkEquipmentSlot networkEquipmentSlot);

        void SetActiveHandEquipmentSet(NetworkActiveHandEquipmentSet networkActiveHandEquipmentSet);

        void InteractWithOvertip(NetworkOvertip networkOvertip);

        EntityDataBase GetEntity(string id);

        bool IsSummoned(string unitId);

        void ApplyPerceptionCheck(NetworkPerceptionCheck networkPerceptionCheck);

        void ApplyInspectionKnowledgeCheck(NetworkInspectionKnowledgeCheck networkInspectionKnowledgeCheck);

        void ApplyStealthPerceptionCheck(NetworkStealthPerceptionCheck networkStealthPerceptionCheck);

        NetworkGameSettings GetGameSettings();

        void ApplyGameSettings(NetworkGameSettings networkGameSettings);

        void SpawnCampPlace(NetworkVector3 position);

        void SetCampingUseHealingSpells(bool isOn);

        void SetCampingState(NetworkCampingState networkCampingState);

        void SetCampingRoles(List<NetworkCampingRole> networkCampingRoles);

        void InitiateRest();

        void UpdateRestUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void StartRest();

        void SetRandomEncounterContext(NetworkRandomEncounterContext networkRandomEncounterContext);

        void TryInterruptRestBanter(NetworkRestBanter networkRestBanter);

        void TransferVendorItem(NetworkVendorItemTransfer networkVendorItemTransfer);

        void CloseVendorWindow();

        void MakeVendorDeal();

        void ForgetSpell(string unitId, NetworkSpellSlot networkSpellSlot, NetworkAbility networkAbility);

        void MemorizeSpell(string unitId, NetworkSpellSlot networkSpellSlot, NetworkAbility networkAbility);

        void MoveActionBarSlots(NetworkActionBarSlot sourceActionBarSlot, NetworkActionBarSlot targetActionBarSlot);

        void ClearActionBarSlot(NetworkActionBarSlot actionBarSlot);

        void LockpickMapObject(NetworkLockpickInteraction lockpickInteraction);

        void ChangeUnitStealth(string unitId, bool isEnabled, bool isForced);

        void UpdateGroupChangerUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void CloseGroupChangerUI();

        void ClickGroupChangerUnit(string unitId);

        void AcceptGroupChangerParty();

        void UpdateSkipTimeUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void CloseSkipTimeUI();

        void OpenSkipTimeUI();

        void CloseRestWindow();

        void UpdateSkipTimeHours(float hours);

        void StartSkipTime();

        void UpdateZoneLootUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        NetworkCampingState GetCampigState();

        void UpdateZoneLootRemoveToggle(bool removeLoot);

        void CompleteZoneLoot();

        NetworkContentState GetInstalledContent();

        void UseInventoryItem(NetworkUseInventoryItem useInventoryItem);

        string GetUnitCharacterName(string unitId);

        void UpdateCharacterSelectionUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void CloseCharacterSelectionWindow();

        void AcceptCharacterSelectionWindow();

        void ToggleCharacterSelectionWindow(string unitId);

        void StartNewGameSequence(string mainCharacterId, Action onBack, Action onStart, Action<NetworkCharacter> onCharacterCreated);

        void SelectNewGameDifficulty(string difficulty);

        void SelectNewGameSequencePhase(NetworkNewGameSequencePhase phase);

        void UpdateNewGameSequencePhaseControls(bool isEnabled, NetworkNewGameSequencePhaseType newGameSequencePhaseType);

        void StartNewGameSequenceLeveling();

        void TerminateNewGameSequence();

        NetworkArea GetCurrentArea();

        /// <summary>
        ///
        /// </summary>
        /// <param name="polymorphicItem"></param>
        /// <param name="createContext">means this operation should be ignored and never sent to other players</param>
        void CreateAndEquipPolymorphicItem(NetworkPolymorphicItem polymorphicItem, bool createContext);

        void SkipCutscene(string playerName);

        void ReselectSelectedCharacters();

        void LeaveZoneLoot();

        void ApplyTrapDisarm(NetworkTrapDisarm trapDisarm);

        bool IsUnitBusy(string unitId);

        void SetUnitAutoUseAbility(NetworkAutoUseAbility autoUseAbility);

        void CopyInventoryItem(NetworkItemCopy itemCopy);
    }
}
