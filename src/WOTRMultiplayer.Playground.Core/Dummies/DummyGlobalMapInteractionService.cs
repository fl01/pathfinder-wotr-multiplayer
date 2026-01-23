using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Entities.GlobalMap;

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

        public void OpenRestMenu()
        {
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

        public void UpdateEnterMessageBoxUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
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
    }
}
