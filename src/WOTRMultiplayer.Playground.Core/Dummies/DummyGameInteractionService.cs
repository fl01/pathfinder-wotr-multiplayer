using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Kingmaker.EntitySystem;
using Kingmaker.GameModes;
using Kingmaker.Items.Slots;
using Kingmaker.UI;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.ActionBar;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.Content;
using WOTRMultiplayer.Entities.Dialogs;
using WOTRMultiplayer.Entities.Equipment;
using WOTRMultiplayer.Entities.GlobalMap;
using WOTRMultiplayer.Entities.Inspect;
using WOTRMultiplayer.Entities.Leveling;
using WOTRMultiplayer.Entities.Loot;
using WOTRMultiplayer.Entities.MapObjects;
using WOTRMultiplayer.Entities.Movement;
using WOTRMultiplayer.Entities.NewGame;
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

        public void AddCombatText(string messageKey, params object[] args)
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

        public void ClearActionBarSlot(NetworkActionBarSlot actionBarSlot)
        {
        }

        public void ClickGroundInCombat(NetworkClick networkClick)
        {
        }

        public void ClickMapObject(NetworkClick networkClick)
        {
        }

        public void ClickUnit(NetworkClick networkClick)
        {
        }

        public void CloseVendorWindow()
        {
        }

        //public void CollectLootContainer(NetworkLootContainer networkLootContainer)
        //{
        //}

        public bool CombatTurnHasBeenFinished()
        {
            return false;
        }

        public void CompleteLeveling()
        {
        }

        public void DecreaseLevelingAbilityScore(NetworkLevelingAbilityScore networkLevelingAbilityScore)
        {
        }

        public void DecreaseLevelingSkillPoint(NetworkLevelingSkillPoint networkLevelingSkillPoint)
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

        public List<NetworkCharacter> GetPartyPlayers()
        {
            return [];
        }

        public string GetPetOwnerId(string unitId)
        {
            return null;
        }

        public string GetSaveGamePath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var fullPath = Path.Combine(appData, "AppData\\LocalLow\\Owlcat Games\\Pathfinder Wrath Of The Righteous\\Saved Games\\");
            return fullPath;
        }

        public NetworkCombatState GetCombatState()
        {
            return null;
        }

        public void IncreaseLevelingAbilityScore(NetworkLevelingAbilityScore networkLevelingAbilityScore)
        {
        }

        public void IncreaseLevelingSkillPoint(NetworkLevelingSkillPoint networkLevelingSkillPoint)
        {
        }

        public void InteractWithOvertip(NetworkOvertip networkOvertip)
        {
        }

        public bool IsSummoned(string unitId)
        {
            return false;
        }

        public bool IsUnitAI(string unitId)
        {
            return false;
        }

        public void LeaveArea(string areaExitId)
        {
        }

        public string LoadGameFromMainMenu(string savePath)
        {
            return string.Empty;
        }

        public void LockpickMapObject(NetworkLockpickInteraction lockpickInteraction)
        {
            throw new NotImplementedException();
        }

        public void MakeVendorDeal()
        {
        }

        public void MarkSuggestedDialogAnswers(List<NetworkDialogAnswerSuggestion> networkDialogAnswerSuggestions)
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

        public void SetPause(bool isPaused)
        {
        }

        public void PlaySound(UISoundType type)
        {
        }

        public string QuickLoadGame(string savePath)
        {
            return null;
        }

        public void RemoveLevelingSpell(NetworkLevelingSpell networkLevelingSpell)
        {
        }

        public void ResetSuggestedDialogAnswers()
        {
        }

        public void SelectDialogAnswer(string dialogName, string cueName, string answerName, string manualUnitSelectionId)
        {
        }

        public void SelectLevelingClass(string classId)
        {
        }

        public void SelectLevelingClassArchetype(string archetypeId)
        {
        }

        public void SelectLevelingFeature(NetworkLevelingFeature networkLevelingFeature)
        {
        }

        public void SelectLevelingSpell(NetworkLevelingSpell networkLevelingSpell)
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

        public void SetDialogContinueButtonState(bool isEnabled)
        {
        }

        public void SetGroundMoveEveryone()
        {
        }

        public void SetRandomEncounterContext(NetworkRandomEncounterContext networkRandomEncounterContext)
        {
        }

        public void UpdateStartRestButtonState(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }

        public void ShowModalMessage(string messageKey, params object[] args)
        {
        }

        public void ShowWarningNotification(string messageKey, params object[] args)
        {
        }

        public void SkinLootContainer(NetworkLootableEntity lootableEntity)
        {
        }

        public void SpawnCampPlace(NetworkVector3 position)
        {
        }

        public Task<bool> StartDialogAsync(string dialogName, string targetUnitId, string initiatorUnitId, string mapObjectId, string speakerKey)
        {
            return Task.FromResult(false);
        }

        public void StartLeveling(string unitId, NetworkLevelingType levelingType)
        {
        }

        public void StartRest()
        {
        }

        public void StartTurnBasedCombatTurn(string unitId)
        {
        }

        public void SwitchLevelingPhase(NetworkLevelingPhase networkLevelingPhase)
        {
        }

        public void TerminateLeveling()
        {
        }

        public void ToggleActivatableAbility(NetworkActivatableAbility networkActivatableAbility)
        {
        }

        public void TransferVendorItem(NetworkVendorItemTransfer networkVendorItemTransfer)
        {
        }

        public void TryInterruptRestBanter(NetworkRestBanter networkRestBanter)
        {
        }

        public void UpdateEquipmentSlot(NetworkEquipmentSlot networkEquipmentSlot)
        {
        }

        public void UpdateIsInCombatStatus()
        {
        }

        public void UpdateLevelingPhaseControls(bool isEnabled)
        {
        }

        public void UseAbility(NetworkAbility networkAbility)
        {
        }

        public Task UpdateCombatStateAsync(NetworkCombatState networkCombatState, bool requiresFullUpdate)
        {
            return Task.CompletedTask;
        }

        public void AttackUnit(NetworkUnitAttack attack)
        {
        }

        public void DelayCombatTurn(string unitId, string targetUnitId)
        {
        }

        public void ChangeUnitStealth(string unitId, bool isEnabled, bool isForced)
        {
        }

        public void UpdateGroupChangerUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }

        public void CloseGroupChangerUI()
        {
        }

        public void ClickGroupChangerUnit(string unitId)
        {
        }

        public void AcceptGroupChangerParty()
        {
        }

        public void OpenGlobalMapRestMenu()
        {
        }

        public void StartGlobalMapTravel(NetworkGlobalMapLocation destination)
        {
        }

        public void UpdateSkipTimeUI(bool canUse, int readyPlayers, int totalPlayers)
        {
        }

        public void CloseSkipTimeUI()
        {
        }

        public void OpenSkipTimeUI()
        {
        }

        public void UpdateSkipTimeHours(float hours)
        {
        }

        public void StartSkipTime()
        {
        }

        public bool IsAtGlobalMapLocation(NetworkGlobalMapLocation globalMapLocation)
        {
            return true;
        }

        public void ContinueGlobalMapTravel(NetworkGlobalMapState globalMapState)
        {
        }

        public void StopGlobalMapTravel(NetworkGlobalMapState globalMapState)
        {
        }

        public void UpdateGlobalMapMessageBoxUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }

        public void UpdateGlobalMapIngredientCollectionUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }

        public void CollectGlobalMapIngredients(NetworkGlobalMapLocation globalMapLocation)
        {
        }

        public void EnterGlobalMapLocation(NetworkGlobalMapLocation globalMapLocation)
        {
        }

        public void UpdateGlobalMapEncounterMessageUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }

        public void AvoidGlobalMapEncounter()
        {
        }

        public void AcceptGlobalMapEncounter()
        {
        }

        public void RollGlobalMapEncounter(NetworkGlobalMapEncounter encounter)
        {
        }

        public NetworkCampingState GetCampigState()
        {
            return null;
        }

        public void UpdateZoneLootUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }

        public void UpdateZoneLootRemoveToggle(bool removeLoot)
        {
        }

        public void CompleteZoneLoot()
        {
        }

        public void TransferInventoryItems(NetworkItemsTransfer networkItemsTransfer)
        {
        }

        public NetworkContentState GetInstalledContent()
        {
            return new NetworkContentState();
        }

        public bool IsInCombat()
        {
            return false;
        }

        public void ApplyStealthPerceptionCheck(NetworkStealthPerceptionCheck networkStealthPerceptionCheck)
        {
        }

        public void UpdateDialogPopupUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }

        public void CloseDialogPopup(NetworkDialogPopup networkDialogPopup)
        {
        }

        public bool CanRiderGetUp()
        {
            return false;
        }

        public bool HasAnyRunningCombatCommands()
        {
            return false;
        }

        public void UseInventoryItem(NetworkUseInventoryItem useInventoryItem)
        {
        }

        public void SelectMythicLevelingClass(string mythicClassId)
        {
        }

        public void SelectLevelingPortrait(NetworkLevelingPortrait levelingPortrait)
        {
        }

        public void SelectLevelingGender(string genderId)
        {
        }

        public void SelectLevelingRace(string raceId)
        {
        }

        public void ChangeLevelingRacialAbilityScoreBonus(NetworkLevelingSequenceDirection direction)
        {
        }

        public void SelectLevelingAlignment(string alignmentId)
        {
        }

        public void SetLevelingName(string name)
        {
        }

        public void ChangeLevelingBirthDay(NetworkLevelingSequenceDirection direction)
        {
        }

        public void ChangeLevelingBirthMonth(NetworkLevelingSequenceDirection direction)
        {
        }

        public void SelectLevelingVoice(NetworkLevelingVoice levelingVoice)
        {
        }

        public void SelectLevelingWarpaintColorAppearance(NetworkLevelingWarpaint levelingWarpaint)
        {
        }

        public void SelectLevelingWarpaintAppearance(NetworkLevelingWarpaint levelingWarpaint)
        {
        }

        public void SelectLevelingTattooColorAppearance(NetworkLevelingTattoo levelingTattoo)
        {
        }

        public void SelectLevelingTattooAppearance(NetworkLevelingTattoo levelingTattoo)
        {
        }

        public void SelectLevelingScarAppearance(int index)
        {
        }

        public void SelectLevelingSecondaryOutfitColorAppearance(string textureName)
        {
        }

        public void SelectLevelingPrimaryOutfitColorAppearance(string textureName)
        {
        }

        public void SelectLevelingHornsColorAppearance(string textureName)
        {
        }

        public void SelectLevelingHornsAppearance(int index)
        {
        }

        public void SelectLevelingHairStyleAppearance(int index)
        {
        }

        public void SelectLevelingHairColorAppearance(string textureName)
        {
        }

        public void SelectLevelingFaceAppearance(int index)
        {
        }

        public void SelectLevelingEyesColorAppearance(string textureName)
        {
        }

        public void SelectLevelingBodyColorAppearance(string textureName)
        {
        }

        public void SelectLevelingBodyTypeAppearance(int index)
        {
        }

        public string GetUnitCharacterName(string unitId)
        {
            return null;
        }

        public void UpdateLevelingRespecUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }

        public void CompleteLevelingRespec()
        {
        }

        public string GetCurrentRespecWindowUnitId()
        {
            return null;
        }

        public void InitiateLevelingRespecLevelUp()
        {
        }

        public void InitiateLevelingRespecMythicLevelUp()
        {
        }

        public void UpdateCharacterSelectionUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }

        public void CloseCharacterSelectionWindow()
        {
        }

        public void AcceptCharacterSelectionWindow()
        {
        }

        public void ToggleCharacterSelectionWindow(string unitId)
        {
        }

        public void StartNewGameSequence(string mainCharacterId, Action onBack, Action onStart, Action<NetworkCharacter> onCharacterCreated)
        {
        }

        public void SelectNewGameDifficulty(string difficulty)
        {
        }

        public void UpdateNewGameSequencePhaseControls(bool isEnabled, NetworkNewGameSequencePhaseType newGameSequencePhaseType)
        {
        }

        public void SelectNewGameSequencePhase(NetworkNewGameSequencePhase phase)
        {
        }

        public void StartNewGameSequenceLeveling()
        {
        }

        public void TerminateNewGameSequence()
        {
        }
    }
}
