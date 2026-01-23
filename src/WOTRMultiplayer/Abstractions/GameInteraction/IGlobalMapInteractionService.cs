using WOTRMultiplayer.Entities.GlobalMap;

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

        void UpdateCombatResultsUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void UpdateCrusadeArmyBattleResultsUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void AcceptCommonPopup(NetworkGlobalMapCommonPopup globalMapCommonPopup);

        void DeclineCommonPopup();

        void EnterLocation(NetworkGlobalMapLocation globalMapLocation);

        void AvoidEncounter();

        void AcceptEncounter();

        void RollEncounter(NetworkGlobalMapEncounter globalMapEncounter);

        void OpenRestMenu();

        void StartTravel(NetworkGlobalMapTravel globalMapTravel);

        void CloseLocationMessageBox();

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

        void DismissCrusadeArmy(NetworkGlobalMapArmy globalMapArmy);

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

        void CreateArmyAtCrusadeArmyInfo();

        void CloseCrusadeArmyMainInfo();

        void SetCrusadeArmyInfoMergeName(NetworkGlobalMapArmy army);

        void SetCrusadeArmyInfoMainName(NetworkGlobalMapArmy army);

        void CloseCrusadeArmySetLeaderInfo();

        void ClearLeaderOnCrusdeArmyInfo();

        void ClickRecruitmentOnSetLeaderScreen();

        void CloseBuyLeaderScreen();

        void UpdateBuyLeaderUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount);
    }
}
