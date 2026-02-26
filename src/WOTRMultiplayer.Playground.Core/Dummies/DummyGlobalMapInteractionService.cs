using System.Threading.Tasks;
using Kingmaker.UI.Kingdom;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Entities.GlobalMap;
using WOTRMultiplayer.Entities.GlobalMap.Kingdom;

namespace WOTRMultiplayer.Playground.Core.Dummies
{
    public class DummyGlobalMapInteractionService : IGlobalMapInteractionService
    {
        public void AcceptEncounter()
        {
        }

        public void AvoidEncounter()
        {
        }

        public void ChangeArmyMode(NetworkGlobalMapTravelerMode travelerMode)
        {
        }

        public void DeclineCommonPopup()
        {
        }

        public void CloseLocationMessageBox()
        {
        }

        public void AcceptCommonPopup(NetworkGlobalMapCommonPopup globalMapCommonPopup)
        {
        }

        public void ContinueTravel(NetworkGlobalMapTraveler globalMapTraveler)
        {
        }

        public void EnterLocation(NetworkGlobalMapLocation globalMapLocation)
        {
        }

        public bool IsAtLocation(NetworkGlobalMapLocation globalMapLocation)
        {
            return false;
        }

        public void RollEncounter(NetworkGlobalMapEncounter encounter)
        {
        }

        public void SetAutoCrusadeCombat(bool isEnabled)
        {
        }

        public void SetSelectedArmy(NetworkGlobalMapArmy globalMapArmy)
        {
        }

        public void SkipDay()
        {
        }

        public void StartTravel(NetworkGlobalMapTravel globalMapTravel)
        {
        }

        public void StopTravel(NetworkGlobalMapTraveler globalMapTraveler)
        {
        }

        public void UpdateEncounterMessageUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }

        public void UpdateCommonPopupUI(NetworkGlobalMapCommonPopup globalMapCommonPopup, bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }

        public void UpdateLocationMessageBoxUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }

        public void UpdateUIState(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }

        public void UpdateCrusadeArmyBattleResultsUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }

        public void CloseCrusadeArmyBattleResults()
        {
        }

        public void StartCrusadeArmyBattleResultsManualCombat()
        {
        }

        public void UpdateCombatResultsUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }

        public void CloseCombatResults()
        {
        }

        public void RunSplitRequestForCrusadeArmySquad(NetworkGlobalMapArmySquadSlot sourceSquadSlot, NetworkGlobalMapArmySquadSlot targetSquadSlot, int count)
        {
        }

        public void SwitchCrusadeArmySquads(NetworkGlobalMapArmySquadSlot sourceSquadSlot, NetworkGlobalMapArmySquadSlot targetSquadSlot)
        {
        }

        public void MergeCrusadeArmySquads(NetworkGlobalMapArmySquadSlot sourceSquadSlot, NetworkGlobalMapArmySquadSlot targetSquadSlot, int count)
        {
        }

        public void SplitCrusadeArmySquad(NetworkGlobalMapArmySquadSlot sourceSquadSlot, int count)
        {
        }

        public void MergeInOneCrusadeArmySquad(NetworkGlobalMapArmySquadSlot sourceSquadSlot)
        {
        }

        public void DismissCrusadeArmySquad(NetworkGlobalMapArmySquadSlot globalMapArmySquadSlot)
        {
        }

        public void UpdateCrusadeArmyInfoUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }

        public void CloseCrusadeArmyInfo()
        {
        }

        public void DismissCrusadeArmy(NetworkGlobalMapArmy globalMapArmy)
        {
        }

        public void MoveCrusadeArmySquadsToMainArmy()
        {
        }

        public void MoveCrusadeArmySquadsToSecondArmy()
        {
        }

        public void SelectPrevCrusadeArmyInfoMergeArmy()
        {
        }

        public void SelectNextCrusadeArmyInfoMergeArmy()
        {
        }

        public void OpenCrusadeArmiesMergeScreen()
        {
        }

        public void OpenCrusadeArmyInfo()
        {
        }

        public void CreateCrusadeArmy()
        {
        }

        public void CloseCrusadeArmyMergeInfo()
        {
        }

        public void CloseCrusadeArmyMainInfo()
        {
        }

        public void SetCrusadeArmyInfoCartName(NetworkGlobalMapArmy army)
        {
        }

        public void CloseCrusadeArmySetLeaderInfo()
        {
        }

        public void ClearLeaderOnCrusdeArmyInfo()
        {
        }

        public void ClickRecruitmentOnSetLeaderScreen()
        {
        }

        public void CloseBuyLeaderScreen()
        {
        }

        public void UpdateBuyLeaderUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }

        public void RunLeaderAction(NetworkGlobalMapArmyLeader globalMapArmyLeader, NetworkGlobalMapArmyLeaderActionType actionType)
        {
        }

        public void UpdateRecruitmentUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }

        public void UpdateSharedCrusadeManagementUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }

        public void SelectNextRecruitmentArmy()
        {
        }

        public void SelectPrevRecruitmentArmy()
        {
        }

        public void RerollRecruitmentMercenaries()
        {
        }

        public void BuyResources(NetworkGlobalMapResourceOrder globalMapResourceOrder)
        {
        }

        public void BuyUnits(NetworkGlobalMapUnitRecruitmentOrder globalMapUnitRecruitmentOrder)
        {
        }

        public void OpenRecruitments()
        {
        }

        public void CloseRecruitments()
        {
        }

        public void UseSpell(NetworkGlobalMapMagicSpell globalMapMagicSpell)
        {
        }

        public void UpdateLeaderLevelingUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }

        public void CloseLeaderLeveling()
        {
        }

        public void ConfirmLeaderLeveling()
        {
        }

        public void SelectLeaderLevelingSkill(string id)
        {
        }

        public void OpenGroupChanger()
        {
        }

        public void StartCrusadeArmyLeaderLeveling(NetworkGlobalMapArmy globalMapArmy)
        {
        }

        public void AcceptLocationMessageBox()
        {
        }

        public Task<bool> ShowCommonPopupAsync(NetworkGlobalMapCommonPopup popup)
        {
            return Task.FromResult(false);
        }

        public void EnterKingdom(NetworkKingdomEntryPoint entryPoint)
        {
        }

        public void ExitKingdom()
        {
        }

        public void UpdateKingdomUIState(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }

        public void ChangeKingdomNavigation(KingdomNavigationType kingdomNavigationType)
        {
        }

        public void SelectKingdomEvent(NetworkKingdomEvent kingdomEvent)
        {
        }

        public void SelectKingdomEventSolution(NetworkKingdomEventSolution kingdomEventSolution)
        {
        }

        public void StartKingdomEvent()
        {
        }

        public void CancelKingdomEvent()
        {
        }

        public void DropKingdomEvent(NetworkKingdomEvent kingdomEvent)
        {
        }

        public void EnterSettlement(NetworkKingdomSettlement kingdomSettlement, bool requiresUnloadEvent, bool exitSettlementToGlobalMap)
        {
        }

        public void LeaveSettlement()
        {
        }

        public void UpdateSettlementUIState(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }

        public void SellSettlementBuilding(NetworkKingdomSettlementBuilding kingdomSettlementBuilding)
        {
        }

        public void BuildSettlementBuilding(NetworkKingdomSettlementBuilding kingdomSettlementBuilding)
        {
        }

        public void Teleport(NetworkGlobalMapLocation location)
        {
        }

        public void UpgradeSettlement(NetworkKingdomSettlement kingdomSettlement)
        {
        }

        public void ShowCurrentEnterCurrentLocationMessage()
        {
        }
    }
}
