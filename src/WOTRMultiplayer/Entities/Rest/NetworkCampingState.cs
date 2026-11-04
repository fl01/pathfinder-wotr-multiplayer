using WOTRMultiplayer.Entities.Items;

namespace WOTRMultiplayer.Entities.Rest
{
    public class NetworkCampingState
    {
        public string CookingBlueprintRecipeId { get; set; }

        public NetworkCraftItem SelectedPotion { get; set; }

        public NetworkCraftItem SelectedScroll { get; set; }

        public bool AutotuneIterationsStatus { get; set; }

        public int IterationsCount { get; set; }
    }
}
