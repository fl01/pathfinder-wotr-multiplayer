using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Entities.GlobalMap;

namespace WOTRMultiplayer.Playground.Core.Dummies
{
    public class DummyGlobalMapInteractionService : IGlobalMapInteractionService
    {
        public void AcceptGlobalMapEncounter()
        {
        }

        public void AvoidGlobalMapEncounter()
        {
        }

        public void CollectGlobalMapIngredients(NetworkGlobalMapLocation globalMapLocation)
        {
        }

        public void ContinueGlobalMapTravel(NetworkGlobalMapState globalMapState)
        {
        }

        public void EnterGlobalMapLocation(NetworkGlobalMapLocation globalMapLocation)
        {
        }

        public bool IsAtGlobalMapLocation(NetworkGlobalMapLocation globalMapLocation)
        {
            return false;
        }

        public void OpenGlobalMapRestMenu()
        {
        }

        public void RollGlobalMapEncounter(NetworkGlobalMapEncounter encounter)
        {
        }

        public void StartGlobalMapTravel(NetworkGlobalMapLocation destination)
        {
        }

        public void StopGlobalMapTravel(NetworkGlobalMapState globalMapState)
        {
        }

        public void UpdateGlobalMapEncounterMessageUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }

        public void UpdateGlobalMapIngredientCollectionUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }

        public void UpdateGlobalMapMessageBoxUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
        }
    }
}
