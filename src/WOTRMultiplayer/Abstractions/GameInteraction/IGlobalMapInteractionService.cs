using WOTRMultiplayer.Entities.GlobalMap;

namespace WOTRMultiplayer.Abstractions.GameInteraction
{
    public interface IGlobalMapInteractionService
    {
        bool IsAtGlobalMapLocation(NetworkGlobalMapLocation globalMapLocation);

        void ContinueGlobalMapTravel(NetworkGlobalMapState globalMapState);

        void StopGlobalMapTravel(NetworkGlobalMapState globalMapState);

        void UpdateGlobalMapMessageBoxUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void UpdateGlobalMapIngredientCollectionUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void CollectGlobalMapIngredients(NetworkGlobalMapLocation globalMapLocation);

        void EnterGlobalMapLocation(NetworkGlobalMapLocation globalMapLocation);

        void UpdateGlobalMapEncounterMessageUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount);

        void AvoidGlobalMapEncounter();

        void AcceptGlobalMapEncounter();

        void RollGlobalMapEncounter(NetworkGlobalMapEncounter encounter);

        void OpenGlobalMapRestMenu();

        void StartGlobalMapTravel(NetworkGlobalMapLocation destination);
    }
}
