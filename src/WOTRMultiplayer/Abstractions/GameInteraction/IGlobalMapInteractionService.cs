using System.Threading.Tasks;
using Kingmaker.UI.Kingdom;
using WOTRMultiplayer.Entities.GlobalMap;
using WOTRMultiplayer.Entities.GlobalMap.Kingdom;

namespace WOTRMultiplayer.Abstractions.GameInteraction
{
    public interface IGlobalMapInteractionService
    {
        bool IsAtLocation(NetworkGlobalMapLocation globalMapLocation);

        void ContinueTravel(NetworkGlobalMapTraveler travaler);

        void StopTravel(NetworkGlobalMapTraveler travaler);

        void UpdateEnterMessageBoxUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void UpdateCommonPopupUI(NetworkGlobalMapCommonPopup globalMapCommonPopup, bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void UpdateEncounterMessageUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void UpdateUIState(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void UpdateKingdomUIState(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void UpdateCombatResultsUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void UpdateCrusadeArmyBattleResultsUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void AcceptCommonPopup(NetworkGlobalMapCommonPopup globalMapCommonPopup);

        void DeclineCommonPopup();

        void EnterLocation(NetworkGlobalMapLocation globalMapLocation);

        void AvoidEncounter();

        void AcceptEncounter();

        void RollEncounter(NetworkGlobalMapEncounter globalMapEncounter);

        void OpenGroupChanger();

        void StartTravel(NetworkGlobalMapTravel globalMapTravel);

        void CloseLocationMessageBox();

        void AcceptLocationMessageBox();

        void SkipDay();

        void SetSelectedArmy(NetworkGlobalMapArmy globalMapArmy);

        void ChangeArmyMode(NetworkGlobalMapTravelerMode travelerMode);

        void SetAutoCrusadeCombat(bool isEnabled);

        void CloseCrusadeArmyBattleResults();

        void StartCrusadeArmyBattleResultsManualCombat();

        void CloseCombatResults();

        void RunSplitRequestForCrusadeArmySquad(NetworkGlobalMapArmySquadSlot sourceSquadSlot, NetworkGlobalMapArmySquadSlot targetSquadSlot, int count);

        void SwitchCrusadeArmySquads(NetworkGlobalMapArmySquadSlot sourceSquadSlot, NetworkGlobalMapArmySquadSlot targetSquadSlot);

        void MergeCrusadeArmySquads(NetworkGlobalMapArmySquadSlot sourceSquadSlot, NetworkGlobalMapArmySquadSlot targetSquadSlot, int count);

        void SplitCrusadeArmySquad(NetworkGlobalMapArmySquadSlot globalMapArmySquadSlot, int count);

        void MergeInOneCrusadeArmySquad(NetworkGlobalMapArmySquadSlot globalMapArmySquadSlot);

        void DismissCrusadeArmySquad(NetworkGlobalMapArmySquadSlot globalMapArmySquadSlot);

        void UpdateCrusadeArmyInfoUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void CloseCrusadeArmyInfo();

        void CloseCrusadeArmyMergeInfo();

        void MoveCrusadeArmySquadsToMainArmy();

        void MoveCrusadeArmySquadsToSecondArmy();

        void SelectPrevCrusadeArmyInfoMergeArmy();

        void SelectNextCrusadeArmyInfoMergeArmy();

        void RunLeaderAction(NetworkGlobalMapArmyLeader globalMapArmyLeader, NetworkGlobalMapArmyLeaderActionType actionType);

        void OpenCrusadeArmiesMergeScreen();

        void OpenCrusadeArmyInfo();

        void CreateCrusadeArmy();

        void CloseCrusadeArmyMainInfo();

        void SetCrusadeArmyInfoCartName(NetworkGlobalMapArmy army);

        void CloseCrusadeArmySetLeaderInfo();

        void ClearLeaderOnCrusdeArmyInfo();

        void ClickRecruitmentOnSetLeaderScreen();

        void CloseBuyLeaderScreen();

        void UpdateBuyLeaderUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void UpdateRecruitmentUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void UpdateSharedCrusadeManagementUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void SelectNextRecruitmentArmy();

        void SelectPrevRecruitmentArmy();

        void RerollRecruitmentMercenaries();

        void BuyResources(NetworkGlobalMapResourceOrder globalMapResourceOrder);

        void BuyUnits(NetworkGlobalMapUnitRecruitmentOrder globalMapUnitRecruitmentOrder);

        void OpenRecruitments();

        void CloseRecruitments();

        void DismissCrusadeArmy(NetworkGlobalMapArmy globalMapArmy);

        void UseSpell(NetworkGlobalMapMagicSpell globalMapMagicSpell);

        void UpdateLeaderLevelingUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void CloseLeaderLeveling();

        void ConfirmLeaderLeveling();

        void SelectLeaderLevelingSkill(string id);

        void StartCrusadeArmyLeaderLeveling(NetworkGlobalMapArmy globalMapArmy);

        Task<bool> ShowCommonPopupAsync(NetworkGlobalMapCommonPopup popup);

        void EnterKingdom(NetworkKingdomEntryPoint entryPoint);

        void ExitKingdom();

        void ChangeKingdomNavigation(KingdomNavigationType kingdomNavigationType);

        void SelectKingdomEvent(NetworkKingdomEvent kingdomEvent);

        void SelectKingdomEventSolution(NetworkKingdomEventSolution kingdomEventSolution);

        void StartKingdomEvent();

        void CancelKingdomEvent();

        void DropKingdomEvent(NetworkKingdomEvent kingdomEvent);

        void EnterSettlement(NetworkKingdomSettlement kingdomSettlement, bool requiresUnloadEvent, bool exitSettlementToGlobalMap);

        void LeaveSettlement();

        void UpdateSettlementUIState(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void SellSettlementBuilding(NetworkKingdomSettlementBuilding kingdomSettlementBuilding);

        void BuildSettlementBuilding(NetworkKingdomSettlementBuilding kingdomSettlementBuilding);
    }
}
