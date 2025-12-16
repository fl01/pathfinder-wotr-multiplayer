using System.Collections.Generic;
using System.Threading.Tasks;
using Kingmaker.EntitySystem;
using Kingmaker.GameModes;
using Kingmaker.Items.Slots;
using Kingmaker.UI;
using WOTRMultiplayer.GameInteraction.Contexts;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.ActionBar;
using WOTRMultiplayer.MP.Entities.Combat;
using WOTRMultiplayer.MP.Entities.Content;
using WOTRMultiplayer.MP.Entities.Dialogs;
using WOTRMultiplayer.MP.Entities.Equipment;
using WOTRMultiplayer.MP.Entities.GlobalMap;
using WOTRMultiplayer.MP.Entities.Inspect;
using WOTRMultiplayer.MP.Entities.Leveling;
using WOTRMultiplayer.MP.Entities.Loot;
using WOTRMultiplayer.MP.Entities.MapObjects;
using WOTRMultiplayer.MP.Entities.Movement;
using WOTRMultiplayer.MP.Entities.Rest;
using WOTRMultiplayer.MP.Entities.Settings;
using WOTRMultiplayer.MP.Entities.Spells;
using WOTRMultiplayer.MP.Entities.Vendor;

namespace WOTRMultiplayer.Abstractions.GameInteraction
{
    public interface IGameInteractionService
    {
        RemoteExecutionContext RemoteContext { get; }

        GameModeType CurrentGameMode { get; }

        void LeaveArea(string areaExitId);

        void MarkSuggestedDialogAnswers(List<NetworkDialogAnswerSuggestion> networkDialogAnswerSuggestions);

        void ResetSuggestedDialogAnswers();

        void MoveNonCombatCharacter(NetworkCharacterMove networkCharacterMove);

        void SetPause(bool isPaused);

        void SelectDialogAnswer(string dialogName, string cueName, string answerName, string manualUnitSelectionId);

        void SetDialogContinueButtonState(bool isEnabled);

        void PlaySound(UISoundType type);

        Task<bool> StartDialogAsync(string dialogName, string targetUnitId, string initiatorUnitId, string mapObjectId, string speakerKey);

        List<NetworkCharacter> GetPartyPlayers();

        void ShowModalMessage(string messageKey, params object[] args);

        void ShowWarningNotification(string messageKey, params object[] args);

        void AddCombatText(string messageKey, params object[] args);

        bool IsUnitAI(string unitId);

        NetworkCombatState GetCombatState();

        string QuickLoadGame(string savePath);

        string LoadGameFromMainMenu(string savePath);

        string GetSaveGamePath();

        string GetPetOwnerId(string unitId);

        void StartTurnBasedCombatTurn(string unitId);

        void EndTurnBasedCombatTurn();

        Task UpdateCombatStateAsync(NetworkCombatState networkCombatState, bool requiresFullUpdate);

        void ClickUnit(NetworkClick networkClick);

        void ClickGroundInCombat(NetworkClick networkClick);

        void ClickMapObject(NetworkClick networkClick);

        bool CombatTurnHasBeenFinished();

        void UseAbility(NetworkAbility networkAbility);

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

        void UpdateStartRestButtonState(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void StartRest();

        void SetRandomEncounterContext(NetworkRandomEncounterContext networkRandomEncounterContext);

        void UpdateIsInCombatStatus();

        void TryInterruptRestBanter(NetworkRestBanter networkRestBanter);

        void SetGroundMoveEveryone();

        void TransferVendorItem(NetworkVendorItemTransfer networkVendorItemTransfer);

        void CloseVendorWindow();

        void MakeVendorDeal();

        void ForgetSpell(NetworkSpellSlot networkSpellSlot);

        void MemorizeSpell(NetworkSpellSlot networkSpellSlot);

        void StartLeveling(string unitId, NetworkLevelingType levelingType);

        void SelectLevelingClassArchetype(string archetypeId);

        void SelectLevelingClass(string classId);

        void UpdateLevelingPhaseControls(bool isEnabled);

        void SwitchLevelingPhase(NetworkLevelingPhase networkLevelingPhase);

        void DecreaseLevelingSkillPoint(NetworkLevelingSkillPoint networkLevelingSkillPoint);

        void IncreaseLevelingSkillPoint(NetworkLevelingSkillPoint networkLevelingSkillPoint);

        void DecreaseLevelingAbilityScore(NetworkLevelingAbilityScore networkLevelingAbilityScore);

        void IncreaseLevelingAbilityScore(NetworkLevelingAbilityScore networkLevelingAbilityScore);

        void SelectLevelingFeature(NetworkLevelingFeature networkLevelingFeature);

        void SelectLevelingSpell(NetworkLevelingSpell networkLevelingSpell);

        void RemoveLevelingSpell(NetworkLevelingSpell networkLevelingSpell);

        void CompleteLeveling();

        void TerminateLeveling();

        void MoveActionBarSlots(NetworkActionBarSlot sourceActionBarSlot, NetworkActionBarSlot targetActionBarSlot);

        void ClearActionBarSlot(NetworkActionBarSlot actionBarSlot);

        void LockpickMapObject(NetworkLockpickInteraction lockpickInteraction);

        void AttackUnit(NetworkUnitAttack attack);

        void DelayCombatTurn(string unitId, string targetUnitId);

        void ChangeUnitStealth(string unitId, bool isEnabled, bool isForced);

        void UpdateGroupChangerUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void CloseGroupChangerUI();

        void ClickGroupChangerUnit(string unitId);

        void AcceptGroupChangerParty();

        void OpenGlobalMapRestMenu();

        void StartGlobalMapTravel(NetworkGlobalMapLocation destination);

        void UpdateSkipTimeUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void CloseSkipTimeUI();

        void OpenSkipTimeUI();

        void UpdateSkipTimeHours(float hours);

        void StartSkipTime();

        bool IsAtGlobalMapLocation(NetworkGlobalMapLocation globalMapLocation);

        void ContinueGlobalMapTravel(NetworkGlobalMapState globalMapState);

        void StopGlobalMapTravel(NetworkGlobalMapState globalMapState);

        void UpdateGlobalMapMessageBoxUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void UpdateGlobalMapIngredientCollectionUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void CollectGlobalMapIngredients(NetworkGlobalMapLocation globalMapLocation);

        void EnterGlobalMapLocation(NetworkGlobalMapLocation globalMapLocation);

        void UpdateGlobalMapEncounterMessageUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void UpdateZoneLootUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void UpdateDialogPopupUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void CloseDialogPopup(NetworkDialogPopup networkDialogPopup);

        void AvoidGlobalMapEncounter();

        void AcceptGlobalMapEncounter();

        void RollGlobalMapEncounter(NetworkGlobalMapEncounter encounter);

        NetworkCampingState GetCampigState();

        void UpdateZoneLootRemoveToggle(bool removeLoot);

        void CompleteZoneLoot();

        NetworkContentState GetInstalledContent();

        bool IsInCombat();

        bool CanRiderGetUp();
    }
}
