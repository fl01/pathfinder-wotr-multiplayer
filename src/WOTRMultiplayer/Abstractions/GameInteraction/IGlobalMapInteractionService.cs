using WOTRMultiplayer.Entities.GlobalMap;

namespace WOTRMultiplayer.Abstractions.GameInteraction
{
    public interface IGlobalMapInteractionService
    {
        bool IsAtLocation(NetworkGlobalMapLocation globalMapLocation);

        void ContinueTravel(NetworkGlobalMapState globalMapState);

        void StopTravel(NetworkGlobalMapState globalMapState);

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

        void SetSelectedArmy(string armyId);

        void ChangeArmyMode(NetworkGlobalMapTravelerMode travelerMode);

        void SetAutoCrusadeCombat(bool isEnabled);

        void CloseCrusadeArmyBattleResults();

        void StartCrusadeArmyAutoBattleResultsManualCombat();

        void CloseCombatBattleResults();
    }
}
