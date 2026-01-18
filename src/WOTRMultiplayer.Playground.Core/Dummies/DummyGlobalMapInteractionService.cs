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

        public void ContinueTravel(NetworkGlobalMapState globalMapState)
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

        public void SetSelectedArmy(string armyId)
        {
        }

        public void SkipDay()
        {
        }

        public void StartTravel(NetworkGlobalMapTravel globalMapTravel)
        {
        }

        public void StopTravel(NetworkGlobalMapState globalMapState)
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

        public void StartCrusadeArmyAutoBattleResultsManualCombat()
        {
        }

        public void UpdateCombatResultsUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }

        public void CloseCombatBattleResults()
        {
        }
    }
}
