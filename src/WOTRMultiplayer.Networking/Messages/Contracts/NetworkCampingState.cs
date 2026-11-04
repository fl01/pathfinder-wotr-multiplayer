using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkCampingState
    {
        [ProtoMember(1)]
        [LogMe]
        public string CookingBlueprintRecipeId { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public NetworkCraftItem SelectedPotion { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public NetworkCraftItem SelectedScroll { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public bool AutotuneIterationsStatus { get; set; }

        [ProtoMember(5)]
        [LogMe]
        public int IterationsCount { get; set; }
    }
}
